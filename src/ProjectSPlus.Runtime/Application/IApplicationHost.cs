using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.Runtime.Application;

public interface IApplicationHost
{
    void Run(AppSettings settings, IWindowScene scene, Action<AppSettings> persistSettings);
}
