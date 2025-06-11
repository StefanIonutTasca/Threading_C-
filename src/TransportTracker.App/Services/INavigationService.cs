using System;
using System.Threading.Tasks;

namespace TransportTracker.App.Services
{
    public interface INavigationService
    {
        /// <summary>
        /// Navigates to a specific route with optional parameters
        /// </summary>
        Task NavigateToAsync(string route, object parameters = null);
        
        /// <summary>
        /// Navigates one step back in the navigation stack
        /// </summary>
        Task GoBackAsync();
        
        /// <summary>
        /// Navigates to the application's shell root
        /// </summary>
        Task NavigateToRootAsync();
        
        /// <summary>
        /// Navigates to a specific route with a navigation state
        /// </summary>
        Task NavigateToAsync(string route, string state);
        
        /// <summary>
        /// Removes the last page from the navigation stack
        /// </summary>
        Task RemoveLastFromBackStackAsync();
    }
}
