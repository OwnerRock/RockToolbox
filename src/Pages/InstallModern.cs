using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RockToolbox.Native
{
    // The Modern look of the Install page: apps on the left (search + WinGet/Chocolatey switch, one scrolling row of categories, a grid of
    // cards that fills the width), and a "Your list" panel on the right (what is selected, the main buttons, "More options").
    // It re-arranges the controls of ui/pages/Install.xaml, so every button keeps its name and its click handler.
    internal sealed partial class InstallPage
    {
        private StackPanel _listRows;
        private FrameworkElement _listEmpty;
        private TextBlock _listPill;
        private readonly Dictionary<string, bool> _shown = new Dictionary<string, bool>();

        private static FrameworkElement El(string name) { return Ui.Find<FrameworkElement>(name); }

        private static void Detach(FrameworkElement e)
        {
            if (e == null) return;
            DependencyObject p = e.Parent;
            Panel pn = p as Panel; if (pn != null) { pn.Children.Remove(e); return; }
            Decorator d = p as Decorator; if (d != null) { d.Child = null; return; }
            ContentControl cc = p as ContentControl; if (cc != null) { cc.Content = null; return; }
        }

        private static Style Res(string key)
        {
            try { return (Style)Ui.Win.FindResource(key); } catch { return null; }
        }

        private static string LabelOf(Button b)
        {
            DockPanel dp = b.Content as DockPanel;
            if (dp != null) foreach (UIElement u in dp.Children) { TextBlock tb = u as TextBlock; if (tb != null && tb.FontFamily.Source != "Segoe MDL2 Assets") return tb.Text; }
            return b.Content as string ?? "";
        }

        private void ModernLayout()
        {
            Grid g = El("pnlInstall") as Grid; if (g == null) return;
            ScrollViewer oldLeft = null; Grid appsGrid = null;
            foreach (UIElement u in g.Children)
            {
                if (u is ScrollViewer) oldLeft = (ScrollViewer)u;
                else if (u is Grid) appsGrid = (Grid)u;
            }
            if (oldLeft == null || appsGrid == null) return;

            Button bInstall = (Button)El("btnInstall"), bStop = (Button)El("btnStopAction"), bUn = (Button)El("btnUninstall"), bUpg = (Button)El("btnUpgrade");
            Button bClear = (Button)El("btnAppsClear"), bColl = (Button)El("btnCollapse"), bExp = (Button)El("btnExpand"), bInst = (Button)El("btnShowInstalled");
            Button bFind = (Button)El("btnShowFind"), bWu = (Button)El("btnShowUpg"), bCustom = (Button)El("btnInstallCustom");
            TextBlock hdrCustom = (TextBlock)El("lblCustomHdr"); TextBox txtCustom = (TextBox)El("txtCustomId"); TextBlock cnt = (TextBlock)El("txtSelCount");
            RadioButton rw = (RadioButton)El("rdPkgWinget"), rc = (RadioButton)El("rdPkgChoco");
            foreach (FrameworkElement e in new FrameworkElement[] { bInstall, bStop, bUn, bUpg, bClear, bColl, bExp, bInst, bFind, bWu, bCustom, hdrCustom, txtCustom, cnt, rw, rc }) Detach(e);

            // ---- left: search + package manager switch, categories in one scrolling row ----
            Grid searchGrid = _search.Parent as Grid;
            StackPanel topStack = _chips.Parent as StackPanel;
            if (searchGrid != null && topStack != null)
            {
                Thickness m = searchGrid.Margin; searchGrid.Margin = new Thickness(0);
                topStack.Children.Remove(searchGrid);
                Grid top = new Grid(); top.Margin = m;
                top.ColumnDefinitions.Add(new ColumnDefinition()); ColumnDefinition cd = new ColumnDefinition(); cd.Width = GridLength.Auto; top.ColumnDefinitions.Add(cd);
                top.Children.Add(searchGrid);
                Border seg = new Border(); seg.CornerRadius = new CornerRadius(12); seg.Background = Ui.Brush("#14171B"); seg.BorderBrush = Ui.Brush("#35506A"); seg.BorderThickness = new Thickness(1);
                seg.Margin = new Thickness(10, 0, 0, 0); seg.Padding = new Thickness(3); seg.Height = 34;
                StackPanel sp = new StackPanel(); sp.Orientation = Orientation.Horizontal;
                Style sr = Res("SegRadio");
                foreach (RadioButton r in new RadioButton[] { rw, rc }) { r.Margin = new Thickness(0); r.ClearValue(Control.ForegroundProperty); r.VerticalContentAlignment = VerticalAlignment.Center; if (sr != null) r.Style = sr; sp.Children.Add(r); }
                seg.Child = sp; Grid.SetColumn(seg, 1); top.Children.Add(seg);
                topStack.Children.Insert(0, top);
            }
            if (topStack != null)
            {
                topStack.Children.Remove(_chips);
                ScrollViewer sv = new ScrollViewer(); sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto; sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                sv.Content = _chips; _chips.Margin = new Thickness(0, 0, 0, 4);
                sv.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e) { sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta); e.Handled = true; };
                topStack.Children.Add(sv);
            }

            // ---- right: "Your list" ----
            Border panel = new Border(); panel.Width = 310; panel.Margin = new Thickness(10, 0, 0, 0); panel.Padding = new Thickness(16, 16, 16, 12); panel.CornerRadius = new CornerRadius(16);
            Style card = Res("Card"); if (card != null) panel.Style = card; panel.Margin = new Thickness(10, 0, 0, 0); panel.CornerRadius = new CornerRadius(16); panel.Padding = new Thickness(16, 16, 16, 12);
            DockPanel dock = new DockPanel(); panel.Child = dock;

            StackPanel head = new StackPanel(); DockPanel.SetDock(head, Dock.Top);
            TextBlock title = new TextBlock(); title.Text = Lang.L("Your list"); title.FontSize = 20; title.FontWeight = FontWeights.Bold; title.Foreground = Ui.Brush("#F2F4FF");
            cnt.HorizontalAlignment = HorizontalAlignment.Left; cnt.FontWeight = FontWeights.Normal; cnt.Foreground = Ui.Brush("#9AA0B8"); cnt.Margin = new Thickness(0, 8, 0, 12);
            Grid titleRow = new Grid();
            titleRow.Children.Add(title);
            Border pill = new Border(); pill.CornerRadius = new CornerRadius(10); pill.Padding = new Thickness(9, 1, 9, 2); pill.HorizontalAlignment = HorizontalAlignment.Right; pill.VerticalAlignment = VerticalAlignment.Center;
            pill.Background = Ui.Brush("#4F8CFF");
            _listPill = new TextBlock(); _listPill.Text = "0"; _listPill.FontSize = 12; _listPill.FontWeight = FontWeights.Bold; _listPill.Foreground = Brushes.White; pill.Child = _listPill;
            titleRow.Children.Add(pill);
            Border strip = new Border(); strip.Height = 3; strip.CornerRadius = new CornerRadius(2); strip.Background = Ui.Brush("#4F8CFF"); strip.Margin = new Thickness(0, 8, 0, 0); strip.Width = 46; strip.HorizontalAlignment = HorizontalAlignment.Left;
            _listTotal = new TextBlock(); _listTotal.FontSize = 12; _listTotal.FontWeight = FontWeights.SemiBold; _listTotal.Foreground = Ui.Brush("#F2F4FF"); _listTotal.Margin = new Thickness(0, 0, 0, 10);
            head.Children.Add(titleRow); head.Children.Add(strip); head.Children.Add(cnt); head.Children.Add(_listTotal);
            dock.Children.Add(head);

            // bottom: main buttons, text buttons, "More options"
            StackPanel bottom = new StackPanel(); bottom.Margin = new Thickness(0, 10, 0, 0); DockPanel.SetDock(bottom, Dock.Bottom);
            bInstall.Margin = new Thickness(0, 0, 0, 6); bStop.Margin = new Thickness(0, 0, 0, 8);
            bInstall.MinHeight = 44; bInstall.FontSize = 14; bInstall.FontWeight = FontWeights.SemiBold;
            bStop.Visibility = Visibility.Collapsed;
            System.Windows.Threading.DispatcherTimer busyTimer = new System.Windows.Threading.DispatcherTimer(); busyTimer.Interval = TimeSpan.FromMilliseconds(350);
            busyTimer.Tick += delegate
            {
                bool run = Work.Busy;
                if (run && bStop.Visibility != Visibility.Visible) { bStop.Visibility = Visibility.Visible; Anim.PopIn(bStop, 0.5, 0.5); }
                else if (!run && bStop.Visibility == Visibility.Visible) bStop.Visibility = Visibility.Collapsed;
            };
            busyTimer.Start();
            panel.Unloaded += delegate { busyTimer.Stop(); };
            bottom.Children.Add(bInstall); bottom.Children.Add(bStop);
            WrapPanel links = new WrapPanel();
            Style lk = Res("LinkBtn");
            foreach (Button b in new Button[] { bClear, bUn, bUpg })
            {
                string label = LabelOf(b); b.Content = label; b.Margin = new Thickness(0, 0, 2, 2);
                if (lk != null) b.Style = lk;
                links.Children.Add(b);
            }
            bottom.Children.Add(links);

            StackPanel more = new StackPanel(); more.Visibility = Visibility.Collapsed; more.Margin = new Thickness(0, 6, 0, 0);
            foreach (Button b in new Button[] { bColl, bExp, bInst, bFind, bWu })
            {
                b.Margin = new Thickness(0, 0, 0, 5);
                more.Children.Add(b);
            }
            hdrCustom.Margin = new Thickness(2, 8, 0, 6); more.Children.Add(hdrCustom);
            txtCustom.Margin = new Thickness(0, 0, 0, 6); more.Children.Add(txtCustom);
            bCustom.Margin = new Thickness(0); more.Children.Add(bCustom);
            ScrollViewer moreScroll = new ScrollViewer(); moreScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; moreScroll.MaxHeight = 250; moreScroll.Content = more; moreScroll.Visibility = Visibility.Collapsed;

            Button toggle = new Button(); toggle.HorizontalAlignment = HorizontalAlignment.Center; toggle.Margin = new Thickness(0, 4, 0, 0);
            if (lk != null) toggle.Style = lk; toggle.Foreground = Ui.Brush("#4FD1FF");
            string moreText = Lang.L("More options");
            toggle.Content = moreText + "  ▾";
            toggle.Click += delegate
            {
                bool open = moreScroll.Visibility != Visibility.Visible;
                if (open)
                {
                    more.Visibility = Visibility.Visible; moreScroll.Visibility = Visibility.Visible; Anim.PopIn(moreScroll, 0.5, 0.0);
                    toggle.Content = moreText + "  ▴";
                }
                else { moreScroll.Visibility = Visibility.Collapsed; toggle.Content = moreText + "  ▾"; }
            };
            bottom.Children.Add(toggle); bottom.Children.Add(moreScroll);
            dock.Children.Add(bottom);

            // middle: the selected apps
            Grid mid = new Grid();
            ScrollViewer listScroll = new ScrollViewer(); listScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _listRows = new StackPanel(); listScroll.Content = _listRows;
            StackPanel empty = new StackPanel(); empty.VerticalAlignment = VerticalAlignment.Center; empty.HorizontalAlignment = HorizontalAlignment.Center; empty.Margin = new Thickness(0, 0, 0, 20);
            TextBlock ei = new TextBlock(); ei.Text = ""; ei.FontFamily = new FontFamily("Segoe MDL2 Assets"); ei.FontSize = 34; ei.Foreground = Ui.Brush("#6B7186"); ei.HorizontalAlignment = HorizontalAlignment.Center;
            TextBlock et = new TextBlock(); et.Text = Lang.L("Pick apps on the left to build your list."); et.TextWrapping = TextWrapping.Wrap; et.TextAlignment = TextAlignment.Center; et.Foreground = Ui.Brush("#9AA0B8"); et.FontSize = 12.5; et.Margin = new Thickness(0, 8, 0, 0); et.MaxWidth = 200;
            empty.Children.Add(ei); empty.Children.Add(et); _listEmpty = empty;
            mid.Children.Add(listScroll); mid.Children.Add(empty);
            dock.Children.Add(mid);

            // ---- put the page together ----
            g.Children.Clear();
            g.ColumnDefinitions.Clear();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            ColumnDefinition right = new ColumnDefinition(); right.Width = GridLength.Auto; g.ColumnDefinitions.Add(right);
            appsGrid.Margin = new Thickness(0);
            Grid.SetColumn(appsGrid, 0); g.Children.Add(appsGrid);
            Grid.SetColumn(panel, 1); g.Children.Add(panel);

            // cards share the free width: 2, 3, 4... per row
            _apps.SizeChanged += delegate { ResizeCards(); };
            RefreshList();
            Anim.PopIn(panel, 1.0, 0.0);
        }

        private void ResizeCards()
        {
            double avail = _apps.ActualWidth;
            if (avail < 100) return;
            int cols = (int)(avail / 250); if (cols < 1) cols = 1;
            double w = Math.Floor(avail / cols) - 8;
            foreach (Card c in _cards) if (Math.Abs(c.Box.Width - w) > 0.5) c.Box.Width = w;
        }

        private void RefreshList()
        {
            if (_listRows == null) return;
            List<string> ids = new List<string>();
            foreach (Card c in _cards) if (_selected.ContainsKey(c.App.Id)) ids.Add(c.App.Id);
            foreach (string k in _selected.Keys) if (!ids.Contains(k)) ids.Add(k);
            _listRows.Children.Clear(); _sizeLabels.Clear();
            if (_listPill != null) _listPill.Text = "" + ids.Count;
            _listEmpty.Visibility = ids.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Dictionary<string, bool> now = new Dictionary<string, bool>();
            int fresh = 0;
            foreach (string id in ids)
            {
                Card card = _cards.Find(delegate(Card x) { return x.App.Id == id; });
                string name = card != null ? card.App.Name : id;
                Border row = new Border(); row.CornerRadius = new CornerRadius(12); row.Background = Ui.Brush("#262B33"); row.Padding = new Thickness(8, 6, 8, 6); row.Margin = new Thickness(0, 0, 0, 6);
                DockPanel dp = new DockPanel();
                string cid = id;
                Border x2 = new Border(); x2.Width = 26; x2.Height = 26; x2.CornerRadius = new CornerRadius(13); x2.Background = Brushes.Transparent; x2.Cursor = Cursors.Hand; x2.ToolTip = Lang.L("Remove");
                Grid xg = new Grid(); xg.Width = 12; xg.Height = 12; xg.HorizontalAlignment = HorizontalAlignment.Center; xg.VerticalAlignment = VerticalAlignment.Center; xg.IsHitTestVisible = false;
                List<System.Windows.Shapes.Rectangle> xl = new List<System.Windows.Shapes.Rectangle>();
                foreach (double ang in new double[] { 45, -45 })
                {
                    System.Windows.Shapes.Rectangle rc = new System.Windows.Shapes.Rectangle(); rc.Width = 13; rc.Height = 2; rc.RadiusX = 1; rc.RadiusY = 1; rc.Fill = Ui.Brush("#C9CDE0");
                    rc.HorizontalAlignment = HorizontalAlignment.Center; rc.VerticalAlignment = VerticalAlignment.Center; rc.RenderTransformOrigin = new Point(0.5, 0.5); rc.RenderTransform = new RotateTransform(ang);
                    xg.Children.Add(rc); xl.Add(rc);
                }
                x2.Child = xg;
                x2.MouseEnter += delegate { x2.Background = Ui.Brush("#4A2A30"); foreach (System.Windows.Shapes.Rectangle q in xl) q.Fill = Ui.Brush("#FF6B6B"); };
                x2.MouseLeave += delegate { x2.Background = Brushes.Transparent; foreach (System.Windows.Shapes.Rectangle q in xl) q.Fill = Ui.Brush("#C9CDE0"); };
                x2.MouseLeftButtonUp += delegate { _selected.Remove(cid); Card cc = _cards.Find(delegate(Card y) { return y.App.Id == cid; }); if (cc != null) Look(cc, false); UpdateCount(); };
                DockPanel.SetDock(x2, Dock.Right);
                Grid ih = new Grid(); ih.Width = 30; ih.Height = 30; ih.Margin = new Thickness(0, 0, 10, 0); DockPanel.SetDock(ih, Dock.Left);
                Border tile = new Border(); tile.CornerRadius = new CornerRadius(9); tile.Background = TileBrush(name);
                TextBlock letter = new TextBlock(); letter.Text = name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?"; letter.FontWeight = FontWeights.Bold; letter.Foreground = Brushes.White; letter.HorizontalAlignment = HorizontalAlignment.Center; letter.VerticalAlignment = VerticalAlignment.Center;
                tile.Child = letter; ih.Children.Add(tile);
                if (card != null && card.Icon != null && card.Icon.Source != null)
                {
                    Border wb = new Border(); wb.CornerRadius = new CornerRadius(9); wb.Background = Brushes.White;
                    Image im = new Image(); im.Source = card.Icon.Source; im.Margin = new Thickness(4); im.Stretch = Stretch.Uniform; wb.Child = im; ih.Children.Add(wb);
                }
                TextBlock nm = new TextBlock(); nm.Text = name; nm.FontWeight = FontWeights.SemiBold; nm.Foreground = Ui.Brush("#F2F4FF"); nm.VerticalAlignment = VerticalAlignment.Center; nm.TextTrimming = TextTrimming.CharacterEllipsis;
                StackPanel nmBox = new StackPanel(); nmBox.VerticalAlignment = VerticalAlignment.Center;
                TextBlock sz = new TextBlock(); sz.FontSize = 11; sz.Foreground = Ui.Brush("#9AA0B8"); sz.Text = SizeText(id);
                nmBox.Children.Add(nm); nmBox.Children.Add(sz);
                if (!_sizeLabels.ContainsKey(id)) _sizeLabels[id] = new List<TextBlock>();
                _sizeLabels[id].Add(sz); WantSize(id);
                dp.Children.Add(x2); dp.Children.Add(ih); dp.Children.Add(nmBox);
                row.Child = dp;
                _listRows.Children.Add(row);
                now[id] = true;
                if (!_shown.ContainsKey(id)) { Fx.FadeUp(row, 20 + fresh * 40, 10); fresh++; }
            }
            _shown.Clear(); foreach (string k in now.Keys) _shown[k] = true;
            UpdateTotal();
        }

        private readonly Dictionary<string, List<TextBlock>> _sizeLabels = new Dictionary<string, List<TextBlock>>();
        private TextBlock _listTotal;

        private static string SizeText(string id)
        {
            long s;
            if (!AppSize.TryCached(id, out s)) return Lang.L("Checking size...");
            return s > 0 ? AppSize.Format(s) : Lang.L("Size unknown");
        }

        private void WantSize(string id)
        {
            long s;
            if (AppSize.TryCached(id, out s)) return;
            string cid = id;
            AppSize.Fetch(cid, delegate(long size)
            {
                Ui.Post(delegate
                {
                    List<TextBlock> l;
                    if (_sizeLabels.TryGetValue(cid, out l)) foreach (TextBlock t in l) t.Text = SizeText(cid);
                    UpdateTotal();
                });
            });
        }

        private void UpdateTotal()
        {
            if (_listTotal == null) return;
            long total = 0; int known = 0, n = 0;
            foreach (string k in _selected.Keys)
            {
                n++; long s;
                if (AppSize.TryCached(k, out s) && s > 0) { total += s; known++; }
            }
            if (n == 0) { _listTotal.Text = ""; return; }
            _listTotal.Text = Lang.L("Total download") + ": " + (known > 0 ? AppSize.Format(total) : "-") + (known < n ? "  (" + known + "/" + n + ")" : "");
        }
    }
}
