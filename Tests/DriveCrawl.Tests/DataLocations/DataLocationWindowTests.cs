using DriveCrawl.Tests.TestSupport;
using System.Windows;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.ViewModels;
using DriveCrawl_Win32_App.Views;

namespace DriveCrawl.Tests.DataLocations;

public sealed class DataLocationWindowTests : IDisposable
{
    private readonly DataLocationTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [WpfFact]
    public void Window_LoadsItsXaml_AndBindsToTheViewModel()
    {
        WpfTestApplication.EnsureCreated();
        var service = _context.CreateService();
        service.SaveChoice(DataLocation.AppFolder);
        var vm = new DataLocationViewModel(service, DataLocationPromptMode.Change);

        var window = new DataLocationWindow(vm);

        window.DataContext.ShouldBeSameAs(vm);
        window.Close();
    }

    [WpfFact]
    public void Constructor_NullViewModel_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new DataLocationWindow(null!));
    }
}
