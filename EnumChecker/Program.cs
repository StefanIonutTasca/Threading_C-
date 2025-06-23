using Microsoft.Extensions.Caching.Memory;
using System;

Console.WriteLine("Available CacheItemPriority values:");
foreach(var value in Enum.GetValues(typeof(CacheItemPriority)))
{
    Console.WriteLine($"- {value}");
}
