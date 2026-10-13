# ------------------------------------------------------------------
$script:Selected  = @{}
$script:Sections  = New-Object System.Collections.ArrayList
$script:AppCards  = New-Object System.Collections.ArrayList
$script:Chips     = New-Object System.Collections.ArrayList
$script:CatFilter = 'All'
$palette = '#E5484D', '#F5A524', '#17C964', '#0E8AAA', '#7B5CFF', '#F31260', '#06B6D4', '#8E4EC6', '#3E63DD', '#12A594'

function Set-CardLook {
    param($Card, [bool]$On)
    if ($On) {
        Set-Res $Card 'Background' '#1E3A4C'; Set-Res $Card 'BorderBrush' '#4FD1FF'
        try {
            $fx = New-Object System.Windows.Media.Effects.DropShadowEffect
            $fx.Color = [System.Windows.Media.ColorConverter]::ConvertFromString([string]$script:Pal['4FD1FF'])
            $fx.BlurRadius = 14; $fx.ShadowDepth = 0; $fx.Opacity = 0.55
            $Card.Effect = $fx
        } catch { }
    } else {
        Set-Res $Card 'Background' '#23272D'; Set-Res $Card 'BorderBrush' '#343A44'
        $Card.Effect = $null
    }
}
function Update-SelCount { $txtSelCount.Text = "Selected Apps: $($script:Selected.Count)" }
function Toggle-AppCard {
    param($Card)
    $id = $Card.Tag.Id
    if ($script:Selected.ContainsKey($id)) { $script:Selected.Remove($id) } else { $script:Selected[$id] = $true }
    Set-CardLook $Card ($script:Selected.ContainsKey($id))
    Update-SelCount
}

# ---- card animations (hover lift, press, selection pop, fade-in)
