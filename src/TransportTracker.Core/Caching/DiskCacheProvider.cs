using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransportTracker.Core.Caching
{
    /// <summary>
    /// Thread-safe disk-based cache implementation
    /// </summary>
    /// <typeparam name="TKey">The type of keys used for cache entries</typeparam>
    /// <typeparam name="TValue">The type of values stored in the cache</typeparam>
    public class DiskCacheProvider<TKey, TValue> : ICacheProvider<TKey, TValue>, IDisposable where TKey : notnull
    {
        private readonly string _cacheDirectory;
        private readonly ILogger _logger;
        private readonly ReaderWriterLockSlim _directoryLock;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
        private readonly long _diskSpaceLimit;
        private readonly JsonSerializerOptions _jsonOptions;
        
        /// <summary>
        /// Creates a new instance of DiskCacheProvider
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="cacheDirectory">Optional custom cache directory</param>
        /// <param name="diskSpaceLimit">Optional disk space limit in bytes</param>
        public DiskCacheProvider(
            ILogger logger,
            string cacheDirectory = null,
            long diskSpaceLimit = 1073741824) // 1GB default
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheDirectory = cacheDirectory ?? 
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TransportTracker",
                    "Cache",
                    typeof(TValue).Name);
            _diskSpaceLimit = diskSpaceLimit;
            _directoryLock = new ReaderWriterLockSlim();
            _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false
            };
            
            // Ensure cache directory exists
            EnsureCacheDirectoryExists();
            
            _logger.LogInformation(
                "Disk cache provider initialized for {ValueType} in directory {Directory} with limit {DiskLimit}MB",
                typeof(TValue).Name,
                _cacheDirectory,
                _diskSpaceLimit / 1048576);
        }
        
        /// <summary>
        /// Gets an item from the disk cache
        /// </summary>
        /// <param name="key">The key of the item to get</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The cached item, or default if not found</returns>
        public async Task<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var cacheFilePath = GetCacheFilePath(key);
            
            // Fast check if file exists
            if (!File.Exists(cacheFilePath))
            {
                _logger.LogDebug("Cache miss for key {Key} (file does not exist)", key);
                return default;
            }
            
            // Acquire file-specific lock for thread safety
            var fileLock = _fileLocks.GetOrAdd(cacheFilePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync(cancellationToken);
            try
            {
                // Check again after acquiring lock (double-check pattern)
                if (!File.Exists(cacheFilePath))
                {
                    _logger.LogDebug("Cache miss for key {Key} (file does not exist after lock)", key);
                    return default;
                }
                
                // Read the cache entry
                using FileStream fileStream = new FileStream(
                    cacheFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);
                
                // First deserialize the wrapper to check expiration
                var diskCacheEntry = await JsonSerializer.DeserializeAsync<DiskCacheEntry<TValue>>(
                    fileStream, 
                    _jsonOptions, 
                    cancellationToken);
                
                // Check if expired
                if (diskCacheEntry == null || IsExpired(diskCacheEntry))
                {
                    _logger.LogDebug("Cache entry expired for key {Key}", key);
                    // Remove the expired file
                    await RemoveFileAsync(cacheFilePath);
                    return default;
                }
                
                // Update last access time
                diskCacheEntry.LastAccessTime = DateTime.UtcNow;
                await UpdateEntryMetadataAsync(cacheFilePath, diskCacheEntry, cancellationToken);
                
                _logger.LogTrace("Cache hit for key {Key} from disk", key);
                return diskCacheEntry.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading cache entry for key {Key}", key);
                return default;
            }
            finally
            {
                fileLock.Release();
            }
        }
        
        /// <summary>
        /// Sets an item in the disk cache with the specified options
        /// </summary>
        /// <param name="key">The key of the item to set</param>
        /// <param name="value">The value to cache</param>
        /// <param name="options">Caching options</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task SetAsync(TKey key, TValue value, CacheEntryOptions options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            if (options == null)
            {
                options = CacheEntryOptions.Standard();
            }
            
            // Skip if memory-only tier
            if (options.Tier == CacheTier.MemoryOnly)
            {
                _logger.LogTrace("Skipping disk cache for MemoryOnly tier, key {Key}", key);
                return;
            }
            
            var cacheFilePath = GetCacheFilePath(key);
            
            // Ensure we have enough disk space
            await EnsureDiskSpaceAsync(options.Size, cancellationToken);
            
            // Acquire file-specific lock for thread safety
            var fileLock = _fileLocks.GetOrAdd(cacheFilePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync(cancellationToken);
            try
            {
                // Create the cache entry
                var diskCacheEntry = new DiskCacheEntry<TValue>
                {
                    Value = value,
                    CreationTime = DateTime.UtcNow,
                    LastAccessTime = DateTime.UtcNow,
                    AbsoluteExpiration = options.AbsoluteExpiration,
                    SlidingExpiration = options.SlidingExpiration,
                    Size = options.Size ?? GetEstimatedSize(value),
                    Priority = options.Priority
                };
                
                // Ensure directory exists
                EnsureCacheDirectoryExists();
                
                // Write to disk
                using FileStream fileStream = new FileStream(
                    cacheFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true);
                
                await JsonSerializer.SerializeAsync(
                    fileStream,
                    diskCacheEntry,
                    _jsonOptions,
                    cancellationToken);
                
                _logger.LogDebug("Added item to disk cache with key {Key}, size {Size} bytes", key, diskCacheEntry.Size);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing cache entry for key {Key}", key);
            }
            finally
            {
                fileLock.Release();
            }
        }
        
        /// <summary>
        /// Removes an item from the disk cache
        /// </summary>
        /// <param name="key">The key of the item to remove</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the item was removed, false if it wasn't in the cache</returns>
        public async Task<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            var cacheFilePath = GetCacheFilePath(key);
            
            // Quick check if file exists
            if (!File.Exists(cacheFilePath))
            {
                return false;
            }
            
            // Acquire file lock
            var fileLock = _fileLocks.GetOrAdd(cacheFilePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync(cancellationToken);
            try
            {
                // Check again after acquiring lock
                if (!File.Exists(cacheFilePath))
                {
                    return false;
                }
                
                await RemoveFileAsync(cacheFilePath);
                _logger.LogDebug("Removed item from disk cache with key {Key}", key);
                return true;
            }
            finally
            {
                fileLock.Release();
                
                // Clean up the lock if no longer needed
                if (_fileLocks.TryRemove(cacheFilePath, out var removedLock))
                {
                    removedLock.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Checks if an item exists in the disk cache
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if the item exists, otherwise false</returns>
        public async Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            var cacheFilePath = GetCacheFilePath(key);
            
            // Quick check if file exists
            if (!File.Exists(cacheFilePath))
            {
                return false;
            }
            
            // We need to check if the file is expired
            var fileLock = _fileLocks.GetOrAdd(cacheFilePath, _ => new SemaphoreSlim(1, 1));
            await fileLock.WaitAsync(cancellationToken);
            try
            {
                // Check again after acquiring lock
                if (!File.Exists(cacheFilePath))
                {
                    return false;
                }
                
                // Open the file and check expiration
                using FileStream fileStream = new FileStream(
                    cacheFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                
                var diskCacheEntry = await JsonSerializer.DeserializeAsync<DiskCacheEntry<TValue>>(
                    fileStream, _jsonOptions, cancellationToken);
                
                if (diskCacheEntry == null || IsExpired(diskCacheEntry))
                {
                    // Expired, remove it
                    await RemoveFileAsync(cacheFilePath);
                    return false;
                }
                
                // Valid entry exists
                return true;
            }
            catch
            {
                // Error reading file, assume it doesn't exist or is corrupt
                return false;
            }
            finally
            {
                fileLock.Release();
            }
        }
        
        /// <summary>
        /// Clears all items from the disk cache
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _directoryLock.EnterWriteLock();
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    return;
                }
                
                // Get all files
                string[] files = Directory.GetFiles(_cacheDirectory, "*.cache");
                
                // Release the directory lock while we delete files
                _directoryLock.ExitWriteLock();
                
                // Delete all files
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await RemoveFileAsync(file);
                }
                
                // Log the result
                _logger.LogInformation("Cleared {Count} items from disk cache", files.Length);
            }
            finally
            {
                // Ensure we release the lock if we're still holding it
                if (_directoryLock.IsWriteLockHeld)
                {
                    _directoryLock.ExitWriteLock();
                }
                
                // Clean up all file locks
                foreach (var lockPair in _fileLocks)
                {
                    lockPair.Value.Dispose();
                }
                _fileLocks.Clear();
            }
        }
        
        /// <summary>
        /// Gets or sets an item in the disk cache, using a factory method to create it if not found
        /// </summary>
        /// <param name="key">The key of the item</param>
        /// <param name="valueFactory">Factory method to create the item if not found</param>
        /// <param name="options">Caching options</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The cached or created value</returns>
        public async Task<TValue> GetOrCreateAsync(
            TKey key, 
            Func<Task<TValue>> valueFactory, 
            CacheEntryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }
            
            if (options == null)
            {
                options = CacheEntryOptions.Standard();
            }
            
            // Skip disk cache for memory-only tier
            if (options.Tier == CacheTier.MemoryOnly)
            {
                _logger.LogTrace("Skipping disk cache for MemoryOnly tier, key {Key}", key);
                return await valueFactory();
            }
            
            // Try to get from cache first
            var result = await GetAsync(key, cancellationToken);
            if (result != null)
            {
                return result;
            }
            
            // Not found in cache, create a key-specific lock
            string lockKey = key.ToString();
            var keyLock = _fileLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await keyLock.WaitAsync(cancellationToken);
            try
            {
                // Double check after acquiring lock
                result = await GetAsync(key, cancellationToken);
                if (result != null)
                {
                    return result;
                }
                
                // Create new value
                _logger.LogDebug("Creating new cache value for key {Key} using factory method", key);
                result = await valueFactory();
                
                // Cache the result if not null
                if (result != null)
                {
                    await SetAsync(key, result, options, cancellationToken);
                }
                
                return result;
            }
            finally
            {
                keyLock.Release();
                
                // Clean up the lock if no longer needed
                if (_fileLocks.TryRemove(lockKey, out var removedLock))
                {
                    removedLock.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Disposes the disk cache provider resources
        /// </summary>
        public void Dispose()
        {
            _directoryLock?.Dispose();
            
            // Dispose all semaphores
            foreach (var lockPair in _fileLocks)
            {
                lockPair.Value.Dispose();
            }
            _fileLocks.Clear();
            
            _logger.LogInformation("Disk cache provider disposed for {Type}", typeof(TValue).Name);
        }
        
        private void EnsureCacheDirectoryExists()
        {
            _directoryLock.EnterWriteLock();
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    Directory.CreateDirectory(_cacheDirectory);
                }
            }
            finally
            {
                _directoryLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Gets the file path for a cache key
        /// </summary>
        private string GetCacheFilePath(TKey key)
        {
            // Generate a safe filename from the key
            string fileName = key.ToString();
            
            // Replace invalid chars
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            
            // Add hash to ensure uniqueness and limit length
            string hash = key.GetHashCode().ToString("X8");
            string fileNameWithHash = $"{fileName}_{hash}.cache";
            
            // Ensure the filename isn't too long
            if (fileNameWithHash.Length > 100)
            {
                // Truncate if needed
                fileNameWithHash = $"{fileNameWithHash.Substring(0, 90)}_{hash}.cache";
            }
            
            return Path.Combine(_cacheDirectory, fileNameWithHash);
        }
        
        /// <summary>
        /// Checks if a cache entry has expired
        /// </summary>
        private bool IsExpired(DiskCacheEntry<TValue> entry)
        {
            if (entry == null)
            {
                return true;
            }
            
            // Check absolute expiration
            if (entry.AbsoluteExpiration.HasValue && 
                entry.AbsoluteExpiration.Value <= DateTimeOffset.Now)
            {
                return true;
            }
            
            // Check sliding expiration
            if (entry.SlidingExpiration.HasValue && 
                entry.LastAccessTime.Add(entry.SlidingExpiration.Value) <= DateTime.UtcNow)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Removes a file from disk
        /// </summary>
        private async Task RemoveFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }
            
            try
            {
                await Task.Run(() => File.Delete(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting cache file {FilePath}", filePath);
            }
        }
        
        /// <summary>
        /// Updates the metadata of a cache entry without changing the value
        /// </summary>
        private async Task UpdateEntryMetadataAsync(string filePath, DiskCacheEntry<TValue> entry, CancellationToken cancellationToken)
        {
            if (!File.Exists(filePath))
            {
                return;
            }
            
            try
            {
                using FileStream fileStream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true);
                
                await JsonSerializer.SerializeAsync(
                    fileStream,
                    entry,
                    _jsonOptions,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cache entry metadata for {FilePath}", filePath);
            }
        }
        
        /// <summary>
        /// Gets an estimated size for a value
        /// </summary>
        private long GetEstimatedSize(TValue value)
        {
            try
            {
                // A rough way to estimate size is to serialize to memory
                using MemoryStream ms = new MemoryStream();
                JsonSerializer.Serialize(ms, value, _jsonOptions);
                return ms.Length;
            }
            catch
            {
                // Default size if we can't determine it
                return 1024; // 1KB default
            }
        }
        
        /// <summary>
        /// Ensures there's enough disk space for a new entry
        /// </summary>
        private async Task EnsureDiskSpaceAsync(long? requiredSize, CancellationToken cancellationToken)
        {
            if (requiredSize == null || requiredSize.Value <= 0)
            {
                return;
            }
            
            _directoryLock.EnterUpgradeableReadLock();
            try
            {
                // Check current disk usage
                long currentUsage = GetCurrentDiskUsage();
                
                // If we have enough space, return
                if (currentUsage + requiredSize.Value <= _diskSpaceLimit)
                {
                    return;
                }
                
                // We need to free up space
                _logger.LogInformation(
                    "Need to free up disk space. Current: {Current}MB, Required: {Required}MB, Limit: {Limit}MB",
                    currentUsage / 1048576,
                    requiredSize.Value / 1048576,
                    _diskSpaceLimit / 1048576);
                
                await FreeDiskSpaceAsync(requiredSize.Value, cancellationToken);
            }
            finally
            {
                _directoryLock.ExitUpgradeableReadLock();
            }
        }
        
        /// <summary>
        /// Gets the current disk usage of the cache
        /// </summary>
        private long GetCurrentDiskUsage()
        {
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    return 0;
                }
                
                DirectoryInfo dirInfo = new DirectoryInfo(_cacheDirectory);
                return dirInfo.EnumerateFiles("*.cache", SearchOption.TopDirectoryOnly)
                    .Sum(file => file.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating disk usage");
                return 0;
            }
        }
        
        /// <summary>
        /// Frees up disk space by removing least recently used entries
        /// </summary>
        private async Task FreeDiskSpaceAsync(long requiredSize, CancellationToken cancellationToken)
        {
            _directoryLock.EnterWriteLock();
            try
            {
                if (!Directory.Exists(_cacheDirectory))
                {
                    return;
                }
                
                DirectoryInfo dirInfo = new DirectoryInfo(_cacheDirectory);
                FileInfo[] cacheFiles = dirInfo.GetFiles("*.cache")
                    .OrderBy(f => f.LastAccessTime)  // Order by last access (oldest first)
                    .ToArray();
                
                long freedSpace = 0;
                foreach (FileInfo file in cacheFiles)
                {
                    // Check if we've freed enough space
                    if (GetCurrentDiskUsage() + requiredSize - freedSpace <= _diskSpaceLimit)
                    {
                        break;
                    }
                    
                    // Skip high-priority items if there are enough lower-priority ones
                    if (IsCacheEntryHighPriority(file.FullName) && 
                        cacheFiles.Length > 10 &&    // If we have many files
                        freedSpace < requiredSize)    // And we haven't freed enough space yet
                    {
                        continue; // Skip and try next file
                    }
                    
                    // Delete this file
                    long fileSize = file.Length;
                    await RemoveFileAsync(file.FullName);
                    freedSpace += fileSize;
                    
                    _logger.LogDebug("Removed cache file {FileName} to free {Size}KB", 
                        file.Name, fileSize / 1024);
                }
            }
            finally
            {
                _directoryLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Checks if a cache entry has high priority
        /// </summary>
        private bool IsCacheEntryHighPriority(string filePath)
        {
            try
            {
                using FileStream fileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                
                var diskCacheEntry = JsonSerializer.Deserialize<DiskCacheEntry<TValue>>(fileStream, _jsonOptions);
                
                return diskCacheEntry?.Priority == CacheItemPriority.High || 
                       diskCacheEntry?.Priority == CacheItemPriority.NeverRemove;
            }
            catch
            {
                return false;
            }
        }
    }
}
