using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace TransportTracker.App.Core.MVVM
{
    /// <summary>
    /// Provides a centralized way to locate view models and associate them with views.
    /// This class handles the creation and caching of view models to support MVVM pattern.
    /// </summary>
    public class ViewModelLocator
    {
        private readonly ConcurrentDictionary<Type, object> _viewModelCache = new ConcurrentDictionary<Type, object>();
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelLocator"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve dependencies.</param>
        public ViewModelLocator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Gets a view model of the specified type. 
        /// If the view model is registered as a singleton in the dependency injection container,
        /// it will be created once and cached. Otherwise, a new instance will be created each time.
        /// </summary>
        /// <typeparam name="TViewModel">The type of the view model to get.</typeparam>
        /// <returns>An instance of the requested view model.</returns>
        public TViewModel GetViewModel<TViewModel>() where TViewModel : BaseViewModel
        {
            // First, try to get the view model from the DI container
            var viewModel = _serviceProvider.GetService<TViewModel>();

            // If not registered in DI, create a new instance using Activator
            if (viewModel == null)
            {
                viewModel = Activator.CreateInstance<TViewModel>();
            }

            return viewModel;
        }

        /// <summary>
        /// Gets a view model instance for the specified view type.
        /// Uses naming conventions to resolve the view model type based on the view type.
        /// </summary>
        /// <param name="viewType">The type of the view.</param>
        /// <returns>The view model instance, or null if no matching view model is found.</returns>
        public BaseViewModel GetViewModelForView(Type viewType)
        {
            if (viewType == null)
                return null;

            var viewName = viewType.Name;
            
            // Convert "ViewName" to "ViewNameViewModel"
            var viewModelTypeName = viewName + "Model";
            
            // If the view already has "View" suffix, replace it with "ViewModel"
            if (viewName.EndsWith("View", StringComparison.OrdinalIgnoreCase))
            {
                viewModelTypeName = viewName.Substring(0, viewName.Length - 4) + "ViewModel";
            }
            
            // Look in the same namespace as the view or in the dedicated ViewModels namespace
            var viewModelType = Type.GetType($"{viewType.Namespace}.{viewModelTypeName}") ?? 
                                Type.GetType($"TransportTracker.App.ViewModels.{viewModelTypeName}");

            if (viewModelType == null)
                return null;

            // Get the generic method info and create a closed generic method for the specific view model type
            var method = typeof(ViewModelLocator).GetMethod(nameof(GetViewModel), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method == null)
                return null;

            var genericMethod = method.MakeGenericMethod(viewModelType);
            
            // Invoke the method to get the view model instance
            return genericMethod.Invoke(this, null) as BaseViewModel;
        }

        /// <summary>
        /// Clears the view model cache.
        /// </summary>
        public void ClearCache()
        {
            _viewModelCache.Clear();
        }
    }
}
