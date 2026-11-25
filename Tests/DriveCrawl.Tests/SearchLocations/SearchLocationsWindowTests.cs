using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;
using DriveCrawl_Win32_App.ViewModels;
using DriveCrawl_Win32_App.Views;
using NSubstitute;

namespace DriveCrawl.Tests.SearchLocations;

public class SearchLocationsWindowTests
{
    [WpfFact]
    public void Window_LoadsItsXaml_AndBindsToTheViewModel()
    {
        WpfTestApplication.EnsureCreated();
        var store = Substitute.For<ISearchLocationStore>();
        store.GetAll().Returns(new[] { new WatchedFolders { Id = 1, Path = @"D:\Media", Priority = 2 } });
        var vm = new SearchLocationsViewModel(store, Substitute.For<IUserDialogs>(), Substitute.For<IFolderPicker>(), Substitute.For<IPathProbe>());
        vm.Load();

        var window = new SearchLocationsWindow(vm);

        window.DataContext.ShouldBeSameAs(vm);
        window.Close();
    }

    [WpfFact]
    public void Constructor_NullViewModel_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new SearchLocationsWindow(null!));
    }
}
