using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace TransportTracker.App.Services
{
    public class NavigationService : INavigationService
    {
        /// <summary>
        /// Navigates back one step in the navigation stack
        /// </summary>
        public async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// Navigates to the specified route with optional parameters
        /// </summary>
        public async Task NavigateToAsync(string route, object parameters = null)
        {
            if (string.IsNullOrWhiteSpace(route))
                return;

            await Shell.Current.GoToAsync(route, true, parameters != null 
                ? new Dictionary<string, object> { { "Parameter", parameters } }
                : null);
        }

        /// <summary>
        /// Navigates to the specified route with a navigation state
        /// </summary>
        public async Task NavigateToAsync(string route, string state)
        {
            if (string.IsNullOrWhiteSpace(route))
                return;

            if (string.IsNullOrWhiteSpace(state))
                await Shell.Current.GoToAsync(route);
            else
                await Shell.Current.GoToAsync($"{state}{route}");
        }

        /// <summary>
        /// Navigates to the application shell root
        /// </summary>
        public async Task NavigateToRootAsync()
        {
            await Shell.Current.GoToAsync("//");
        }

        /// <summary>
        /// Removes the last page from the navigation stack
        /// </summary>
        public async Task RemoveLastFromBackStackAsync()
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
            {
                var lastPage = Shell.Current.Navigation.NavigationStack[Shell.Current.Navigation.NavigationStack.Count - 2];
                Shell.Current.Navigation.RemovePage(lastPage);
            }
            
            await Task.CompletedTask;
        }
    }
}
