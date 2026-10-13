$btnCollapse.Add_Click({
    foreach ($sec in $script:Sections) { $sec.Wrap.Visibility = 'Collapsed'; $sec.Header.Text = '+ ' + $sec.Cat }
})
