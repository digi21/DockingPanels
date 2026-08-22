namespace Digi21.WinUI.Docking.Primitives;

// What the tab controls of a DockingWindowContainer have in common: the window each one stands
// for. It is all their container needs of them to find a tab again, and all their automation peer
// needs to answer for one.
internal interface IDockingWindowTab
{
    DockingWindow? Window { get; }
}
