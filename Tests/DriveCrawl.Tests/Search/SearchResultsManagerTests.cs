using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;
using static DriveCrawl_Win32_App.MainWindow;

namespace DriveCrawl.Tests.Search;

public class SearchResultsManagerTests
{
    private ObservableCollection<FileViewModel> _results = null!;
    private TextBlock _countLabel = null!;
    private TextBlock _sizeLabel = null!;
    private TextBlock _placeholderLabel = null!;
    private Border _emptyState = null!;

    [WpfFact]
    public void Constructor_NullArguments_Throw()
    {
        var label = new TextBlock();
        var border = new Border();
        var results = new ObservableCollection<FileViewModel>();

        Should.Throw<ArgumentNullException>(() => new SearchResultsManager(null!, label, label, label, label, border));
        Should.Throw<ArgumentNullException>(() => new SearchResultsManager(results, null!, label, label, label, border));
        Should.Throw<ArgumentNullException>(() => new SearchResultsManager(results, label, null!, label, label, border));
        Should.Throw<ArgumentNullException>(() => new SearchResultsManager(results, label, label, null!, label, border));
        Should.Throw<ArgumentNullException>(() => new SearchResultsManager(results, label, label, label, null!, border));
        Should.Throw<ArgumentNullException>(() => new SearchResultsManager(results, label, label, label, label, null!));
    }

    [WpfFact]
    public void SetSearchResults_PopulatesCollectionAndLabels()
    {
        var manager = CreateManager();

        manager.SetSearchResults(new[] { TestData.ViewModel("a.mp4", 1_024), TestData.ViewModel("b.mp4", 2_048) });

        _results.Count.ShouldBe(2);
        _countLabel.Text.ShouldBe("2 results");
        _sizeLabel.Text.ShouldBe("Total size: 3 KB");
        _emptyState.Visibility.ShouldBe(Visibility.Collapsed);
    }

    [WpfFact]
    public void SetSearchResults_SingleResult_UsesSingularLabel()
    {
        var manager = CreateManager();

        manager.SetSearchResults(new[] { TestData.ViewModel("a.mp4", 10) });

        _countLabel.Text.ShouldBe("1 result");
    }

    [WpfFact]
    public void SetSearchResults_Empty_ShowsEmptyState()
    {
        var manager = CreateManager();

        manager.SetSearchResults(Array.Empty<FileViewModel>());

        _countLabel.Text.ShouldBe("0 results");
        _emptyState.Visibility.ShouldBe(Visibility.Visible);
    }

    [WpfFact]
    public void ClearResults_ResetsCollectionAndLabels()
    {
        var manager = CreateManager();
        manager.SetSearchResults(new[] { TestData.ViewModel("a.mp4", 10) });

        manager.ClearResults();

        _results.ShouldBeEmpty();
        _countLabel.Text.ShouldBe("0 results");
        _sizeLabel.Text.ShouldBe("Total size: 0 B");
    }

    [WpfFact]
    public void ApplyFilter_ShowsOnlyMatchingResults()
    {
        var manager = CreateManager();
        manager.SetSearchResults(new[] { TestData.ViewModel("cat.mp4", 1_024), TestData.ViewModel("dog.mp4", 2_048) });

        manager.ApplyFilter("cat", "", Operator.Equals, "ALL", Operator.Equals);

        _results.ShouldHaveSingleItem().FileName.ShouldBe("cat.mp4");
        _countLabel.Text.ShouldBe("1 result");
        _sizeLabel.Text.ShouldBe("Total size: 1 KB");
        _emptyState.Visibility.ShouldBe(Visibility.Collapsed);
    }

    [WpfFact]
    public void ApplyFilter_NoMatches_ShowsPlaceholderMessage()
    {
        var manager = CreateManager();
        manager.SetSearchResults(new[] { TestData.ViewModel("cat.mp4") });

        manager.ApplyFilter("zebra", "", Operator.Equals, "ALL", Operator.Equals);

        _results.ShouldBeEmpty();
        _placeholderLabel.Text.ShouldBe("No results match the current filter criteria.");
        _emptyState.Visibility.ShouldBe(Visibility.Visible);
    }

    [WpfFact]
    public void ApplyFilter_WithNoActiveCriteria_RestoresOriginalResults()
    {
        var manager = CreateManager();
        manager.SetSearchResults(new[] { TestData.ViewModel("cat.mp4"), TestData.ViewModel("dog.mp4") });
        manager.ApplyFilter("cat", "", Operator.Equals, "ALL", Operator.Equals);

        manager.ApplyFilter("", "", Operator.Equals, "ALL", Operator.Equals);

        _results.Count.ShouldBe(2);
        _emptyState.Visibility.ShouldBe(Visibility.Collapsed);
    }

    [WpfFact]
    public void ApplyFilter_AlwaysStartsFromTheOriginalResults_FiltersDoNotStack()
    {
        var manager = CreateManager();
        manager.SetSearchResults(new[] { TestData.ViewModel("cat.mp4"), TestData.ViewModel("dog.mp4") });
        manager.ApplyFilter("cat", "", Operator.Equals, "ALL", Operator.Equals);

        manager.ApplyFilter("dog", "", Operator.Equals, "ALL", Operator.Equals);

        _results.ShouldHaveSingleItem().FileName.ShouldBe("dog.mp4");
    }

    [WpfFact]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    public void ApplyFilter_BringsBackRowsRemovedFromTheCollection()
    {
        // Why pinned: FileActionsManager removes deleted rows from the collection only, so they reappear on the next filter.
        var manager = CreateManager();
        var deleted = TestData.ViewModel("deleted.mp4");
        manager.SetSearchResults(new[] { deleted, TestData.ViewModel("kept.mp4") });
        _results.Remove(deleted);

        manager.ApplyFilter("", "", Operator.Equals, "ALL", Operator.Equals);

        _results.ShouldContain(deleted);
    }

    [WpfFact]
    public void GetSelectedResults_ReturnsOnlySelectedVisibleRows()
    {
        var manager = CreateManager();
        var selectedVisible = TestData.ViewModel("cat-1.mp4");
        var selectedHidden = TestData.ViewModel("dog.mp4");
        selectedVisible.IsSelected = true;
        selectedHidden.IsSelected = true;
        manager.SetSearchResults(new[] { selectedVisible, selectedHidden, TestData.ViewModel("cat-2.mp4") });
        manager.ApplyFilter("cat", "", Operator.Equals, "ALL", Operator.Equals);

        manager.GetSelectedResults().ShouldBe(new[] { selectedVisible });
        manager.GetCurrentResultCount().ShouldBe(2);
    }

    private SearchResultsManager CreateManager()
    {
        _results = new ObservableCollection<FileViewModel>();
        _countLabel = new TextBlock();
        _sizeLabel = new TextBlock();
        _placeholderLabel = new TextBlock();
        _emptyState = new Border();
        return new SearchResultsManager(_results, new TextBlock(), _countLabel, _sizeLabel, _placeholderLabel, _emptyState);
    }
}
