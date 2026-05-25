function Get-DbfColumns($path) {
    if (-not (Test-Path $path)) { return "File not found: $path" }
    $stream = New-Object System.IO.FileStream($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    $reader = New-Object System.IO.BinaryReader($stream)
    $header = $reader.ReadBytes(32)
    $columns = @()
    while ($true) {
        $nextByte = $reader.ReadByte()
        if ($nextByte -eq 0x0D) { break }
        $fieldBytes = @($nextByte) + $reader.ReadBytes(31)
        # Find first null terminator
        $nameLen = 0
        while ($nameLen -lt 11 -and $fieldBytes[$nameLen] -ne 0) {
            $nameLen++
        }
        $nameBytes = $fieldBytes[0..($nameLen-1)]
        $name = [System.Text.Encoding]::ASCII.GetString($nameBytes)
        $type = [char]$fieldBytes[11]
        $length = $fieldBytes[16]
        $decimal = $fieldBytes[17]
        $columns += "$name ($type/$length`::$decimal)"
    }
    $reader.Close()
    $stream.Close()
    return $columns
}

Write-Output "POSL_OBR columns:"
(Get-DbfColumns "C:\PLATA\PLATA\KOR28\POSL_OBR.DBF") -join ", "
Write-Output "`nPOSLOBRI columns:"
(Get-DbfColumns "C:\PLATA\PLATA\KOR28\POSLOBRI.DBF") -join ", "
