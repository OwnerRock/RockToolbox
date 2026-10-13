$script:IconTimer = New-Object System.Windows.Threading.DispatcherTimer
$script:IconTimer.Interval = [TimeSpan]::FromMilliseconds(120)
$script:IconTimer.Add_Tick({ try { Step-IconLoader } catch { } })

function New-AppCard {
    param($App)
    $sum = 0
    foreach ($ch in $App.Name.ToCharArray()) { $sum += [int]$ch }

    $letter = New-Object System.Windows.Controls.TextBlock
    $letter.Text = $App.Name.Substring(0, 1).ToUpper()
    Set-Res $letter 'Foreground' '#FFFFFF'; $letter.FontWeight = 'Bold'; $letter.FontSize = 16
    $letter.HorizontalAlignment = 'Center'; $letter.VerticalAlignment = 'Center'

    $img = New-Object System.Windows.Controls.Image
    $img.Width = 28; $img.Height = 28
    $img.HorizontalAlignment = 'Center'; $img.VerticalAlignment = 'Center'
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($img, [System.Windows.Media.BitmapScalingMode]::HighQuality)

    $grid = New-Object System.Windows.Controls.Grid
    [void]$grid.Children.Add($letter); [void]$grid.Children.Add($img)

    $badge = New-Object System.Windows.Controls.Border
    $badge.Width = 38; $badge.Height = 38
    $badge.CornerRadius = New-Object System.Windows.CornerRadius(19)
    $badge.Background = Get-Brush $palette[$sum % $palette.Count]
    $badge.Margin = '8,0,10,0'; $badge.VerticalAlignment = 'Center'
    $badge.Child = $grid

    if ($App.Dom) { Register-AppIcon @{ Img = $img; Letter = $letter; Badge = $badge; Dom = $App.Dom } }

    $name = New-Object System.Windows.Controls.TextBlock
    $name.Text = $App.Name; $name.FontWeight = 'SemiBold'; Set-Res $name 'Foreground' '#F2F4FF'
    $name.TextTrimming = 'CharacterEllipsis'; $name.MaxWidth = 185

    $inst = New-Object System.Windows.Controls.TextBlock
    $inst.Text = 'installed'; $inst.FontSize = 10; Set-Res $inst 'Foreground' '#3DDC84'; $inst.Visibility = 'Collapsed'

    $stack = New-Object System.Windows.Controls.StackPanel
    $stack.VerticalAlignment = 'Center'
    [void]$stack.Children.Add($name); [void]$stack.Children.Add($inst)

    $row = New-Object System.Windows.Controls.StackPanel
    $row.Orientation = 'Horizontal'
    [void]$row.Children.Add($badge); [void]$row.Children.Add($stack)

    $card = New-Object System.Windows.Controls.Border
    $card.Width = 262; $card.Height = 56; $card.Margin = '4'
    $card.CornerRadius = New-Object System.Windows.CornerRadius(4)
    $card.BorderThickness = New-Object System.Windows.Thickness(1)
    $card.Cursor = [System.Windows.Input.Cursors]::Hand
    $card.ToolTip = $App.Id
    $card.Child = $row
    $App['Badge'] = $inst
    $card.Tag = $App
    Set-CardLook $card $false
    Initialize-CardAnim $card
    $card.Add_MouseEnter({ param($s, $e) Animate-Card $s 1.035 -2 130 })
    $card.Add_MouseLeave({ param($s, $e) Animate-Card $s 1.0 0 170 })
    $card.Add_MouseLeftButtonDown({ param($s, $e) Animate-Card $s 0.96 0 70 })
    $card.Add_MouseLeftButtonUp({ param($s, $e) Toggle-AppCard $s; Animate-Card $s 1.035 -2 300 'back' })
    return $card
}

foreach ($g in ($Apps | Group-Object { $_.Cat })) {
    $wrap = New-Object System.Windows.Controls.WrapPanel
    $hdr = New-Object System.Windows.Controls.TextBlock
    $hdr.Style = $window.FindResource('H')
    $hdr.Text = '- ' + $g.Name
    $hdr.Cursor = [System.Windows.Input.Cursors]::Hand
    $sec = @{ Cat = $g.Name; Wrap = $wrap; Header = $hdr; Panel = $null; Cards = (New-Object System.Collections.ArrayList) }
    $hdr.Tag = $sec
    $hdr.Add_MouseLeftButtonUp({
        param($s, $e)
        $sc = $s.Tag
        if ($sc.Wrap.Visibility -eq 'Visible') { $sc.Wrap.Visibility = 'Collapsed'; $s.Text = '+ ' + $sc.Cat }
        else { $sc.Wrap.Visibility = 'Visible'; $s.Text = '- ' + $sc.Cat; Start-PageAnim $sc.Wrap 10 }
    })
    foreach ($a in $g.Group) {
        $card = New-AppCard $a
        [void]$wrap.Children.Add($card)
        $item = @{ Card = $card; App = $a; Cat = $g.Name; Sec = $sec }
        [void]$sec.Cards.Add($item)
        [void]$script:AppCards.Add($item)
    }
    $panel = New-Object System.Windows.Controls.StackPanel
    [void]$panel.Children.Add($hdr); [void]$panel.Children.Add($wrap)
    $sec.Panel = $panel
    [void]$appsPanel.Children.Add($panel)
    [void]$script:Sections.Add($sec)
}

function Apply-AppFilter {
    $q = $txtAppSearch.Text.Trim()
    $shown = 0
    foreach ($sec in $script:Sections) {
        $any = $false
        foreach ($it in $sec.Cards) {
            $okCat = ($script:CatFilter -eq 'All') -or ($it.Cat -eq $script:CatFilter)
            $okQ   = (-not $q) -or ($it.App.Name -like "*$q*") -or ($it.App.Id -like "*$q*")
            if ($okCat -and $okQ) {
                $wasHidden = ($it.Card.Visibility -ne 'Visible')
                $it.Card.Visibility = 'Visible'; $any = $true
                if ($wasHidden -and $script:CardAnimOn) { Start-CardFade $it.Card ([Math]::Min($shown, 24) * 14); $shown++ }
            } else { $it.Card.Visibility = 'Collapsed' }
        }
        $sec.Panel.Visibility = if ($any) { 'Visible' } else { 'Collapsed' }
    }
}
