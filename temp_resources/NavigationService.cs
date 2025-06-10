using System;
using System.Threading.Tasks;

namespace TransportTracker.UI.Services
{
    public interface INavigationService
    {
        Task NavigateToAsync(string route);
        Task NavigateToAsync(string route, object parameter);
        Task NavigateBackAsync();
        Task NavigateToRootAsync();
    }

    public class NavigationService : INavigationService
    {
        public Task NavigateToAsync(string route)
        {
            if (string.IsNullOrEmpty(route))
                throw new ArgumentNullException(nameof(route));

            return Shell.Current.GoToAsync(route);
        }

        public Task NavigateToAsync(string route, object parameter)
        {
            if (string.IsNullOrEmpty(route))
                throw new ArgumentNullException(nameof(route));

            return Shell.Current.GoToAsync(route, true, new Dictionary<string, object>
            {
                { "Parameter", parameter }
            });
        }

        public Task NavigateBackAsync()
        {
            return Shell.Current.GoToAsync("..");
        }

        public Task NavigateToRootAsync()
        {
            return Shell.Current.GoToAsync("//");
        }
    }
}
