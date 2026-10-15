param([string]$OutputFile=(Join-Path $PSScriptRoot '../Host/Assets/crosshair-community.json'))
$ErrorActionPreference='Stop'
$records=@{}
foreach($game in @('Valorant','Fortnite','Counter-Strike')) {
    $offset=0
    do {
        $url='https://mod.io/v1/games/@crosshair-x/mods?_limit=100&_offset='+$offset+'&tags='+[uri]::EscapeDataString($game)+'&_sort=id'
        $data=Invoke-RestMethod $url -Headers @{'X-Modio-Origin'='web';Accept='application/json'}
        foreach($item in $data.data) {
            if($item.tags.name -notcontains $game) { throw "Server returned an entry outside the requested game tag." }
            $key=[string]$item.id
            if(!$records.ContainsKey($key)) {
                $records[$key]=[ordered]@{Id=$item.id;Name=$item.name;Creator=$item.submitted_by.username;Games=@();Slug=$item.name_id;FileId=$item.modfile.id;Bytes=$item.modfile.filesize;Tags=@($item.tags.name);Rating=$item.stats.ratings_weighted_aggregate}
            }
            if($records[$key].Games -notcontains $game) { $records[$key].Games+=$game }
        }
        $offset+=$data.result_count
    } while($offset -lt $data.result_total -and $data.result_count -gt 0)
    if($offset -ne $data.result_total) { throw "Incomplete catalog response for $game. The bundled catalog has not been replaced." }
    Write-Output "$game`: $offset"
}
$target=[IO.Path]::GetFullPath($OutputFile)
$temporary=$target+'.tmp'
try {
    $records.Values | Sort-Object Id | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $temporary -Encoding utf8
    Move-Item -LiteralPath $temporary -Destination $target -Force
} finally {
    if(Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
Write-Output "Unique catalog entries: $($records.Count)"
