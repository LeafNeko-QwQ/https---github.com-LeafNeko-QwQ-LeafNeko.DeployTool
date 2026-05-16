using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace LeafNeko.DeployTool.Helpers;

public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    private bool _suppress;

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppress)
            base.OnCollectionChanged(e);
    }

    public void ClearAndAddRange(IEnumerable<T> items)
    {
        _suppress = true;
        Clear();
        foreach (var item in items)
            Add(item);
        _suppress = false;
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
