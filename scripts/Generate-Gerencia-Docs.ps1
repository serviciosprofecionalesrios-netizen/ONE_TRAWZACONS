param(
    [string]$OutputDir = ".\Entregables\Gerencia",
    [string]$LogoPath = ".\wwwroot\images\logo-trawzacons.png"
)

$ErrorActionPreference = "Stop"

function Escape-XmlText {
    param([string]$Text)
    if ($null -eq $Text) { return "" }
    return [System.Security.SecurityElement]::Escape($Text)
}

function Ensure-Dir {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Write-Utf8File {
    param(
        [string]$Path,
        [string]$Content
    )
    $dir = Split-Path -Parent $Path
    Ensure-Dir -Path $dir
    Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8
}

function New-Docx {
    param(
        [string]$FilePath,
        [string[]]$Paragraphs,
        [string]$Title
    )

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("docx_" + [Guid]::NewGuid().ToString("N"))
    Ensure-Dir -Path $tempRoot
    Ensure-Dir -Path (Join-Path $tempRoot "_rels")
    Ensure-Dir -Path (Join-Path $tempRoot "word")
    Ensure-Dir -Path (Join-Path $tempRoot "word\_rels")
    Ensure-Dir -Path (Join-Path $tempRoot "docProps")

    $ct = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>
"@

    $rels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
"@

    $docRels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"></Relationships>
"@

    $nowIso = (Get-Date).ToString("s") + "Z"
    $core = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>Instructivo IT y Operaciones</dc:title>
  <dc:creator>IT Service Desk</dc:creator>
  <cp:lastModifiedBy>IT Service Desk</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">$nowIso</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">$nowIso</dcterms:modified>
</cp:coreProperties>
"@

    $app = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Microsoft Office Word</Application>
</Properties>
"@

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
    [void]$sb.AppendLine('<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">')
    [void]$sb.AppendLine('  <w:body>')

    $titleEsc = Escape-XmlText -Text $Title
    [void]$sb.AppendLine('    <w:p><w:r><w:rPr><w:b/><w:sz w:val="34"/></w:rPr><w:t xml:space="preserve">' + $titleEsc + '</w:t></w:r></w:p>')
    [void]$sb.AppendLine('    <w:p><w:r><w:t xml:space="preserve"> </w:t></w:r></w:p>')

    foreach ($line in $Paragraphs) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            [void]$sb.AppendLine('    <w:p><w:r><w:t xml:space="preserve"> </w:t></w:r></w:p>')
            continue
        }
        $esc = Escape-XmlText -Text $line
        if ($line.StartsWith("## ")) {
            $clean = Escape-XmlText -Text $line.Substring(3)
            [void]$sb.AppendLine('    <w:p><w:r><w:rPr><w:b/><w:sz w:val="28"/></w:rPr><w:t xml:space="preserve">' + $clean + '</w:t></w:r></w:p>')
        } else {
            [void]$sb.AppendLine('    <w:p><w:r><w:t xml:space="preserve">' + $esc + '</w:t></w:r></w:p>')
        }
    }

    [void]$sb.AppendLine('    <w:sectPr>')
    [void]$sb.AppendLine('      <w:pgSz w:w="11906" w:h="16838"/>')
    [void]$sb.AppendLine('      <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/>')
    [void]$sb.AppendLine('    </w:sectPr>')
    [void]$sb.AppendLine('  </w:body>')
    [void]$sb.AppendLine('</w:document>')

    Write-Utf8File -Path (Join-Path $tempRoot "[Content_Types].xml") -Content $ct
    Write-Utf8File -Path (Join-Path $tempRoot "_rels\.rels") -Content $rels
    Write-Utf8File -Path (Join-Path $tempRoot "word\document.xml") -Content $sb.ToString()
    Write-Utf8File -Path (Join-Path $tempRoot "word\_rels\document.xml.rels") -Content $docRels
    Write-Utf8File -Path (Join-Path $tempRoot "docProps\core.xml") -Content $core
    Write-Utf8File -Path (Join-Path $tempRoot "docProps\app.xml") -Content $app

    if (Test-Path -LiteralPath $FilePath) {
        Remove-Item -LiteralPath $FilePath -Force
    }
    $zipPath = [System.IO.Path]::ChangeExtension($FilePath, ".zip")
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $tempRoot "*") -DestinationPath $zipPath -Force
    Move-Item -LiteralPath $zipPath -Destination $FilePath -Force
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

function New-SlideXml {
    param(
        [string]$Title,
        [string[]]$Lines,
        [bool]$IncludeLogo = $false
    )

    $titleEsc = Escape-XmlText -Text $Title
    $bodyParagraphs = New-Object System.Text.StringBuilder
    $first = $true
    foreach ($line in $Lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $lineEsc = Escape-XmlText -Text $line
        if ($first) {
            [void]$bodyParagraphs.AppendLine("<a:p><a:r><a:rPr lang='es-NI' sz='2200'/><a:t>$lineEsc</a:t></a:r></a:p>")
            $first = $false
        } else {
            [void]$bodyParagraphs.AppendLine("<a:p><a:pPr marL='342900' indent='-285750'><a:buChar char='•'/></a:pPr><a:r><a:rPr lang='es-NI' sz='1800'/><a:t>$lineEsc</a:t></a:r></a:p>")
        }
    }

    $logoShape = ""
    if ($IncludeLogo) {
        $logoShape = @"
      <p:pic>
        <p:nvPicPr>
          <p:cNvPr id="4" name="Logo Trawzacons"/>
          <p:cNvPicPr/>
          <p:nvPr/>
        </p:nvPicPr>
        <p:blipFill>
          <a:blip r:embed="rId2"/>
          <a:stretch><a:fillRect/></a:stretch>
        </p:blipFill>
        <p:spPr>
          <a:xfrm><a:off x="10312400" y="152400"/><a:ext cx="1524000" cy="685800"/></a:xfrm>
          <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
        </p:spPr>
      </p:pic>
"@
    }

    return @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld>
    <p:spTree>
      <p:nvGrpSpPr>
        <p:cNvPr id="1" name=""/>
        <p:cNvGrpSpPr/>
        <p:nvPr/>
      </p:nvGrpSpPr>
      <p:grpSpPr>
        <a:xfrm>
          <a:off x="0" y="0"/>
          <a:ext cx="0" cy="0"/>
          <a:chOff x="0" y="0"/>
          <a:chExt cx="0" cy="0"/>
        </a:xfrm>
      </p:grpSpPr>
      <p:sp>
        <p:nvSpPr>
          <p:cNvPr id="2" name="Title"/>
          <p:cNvSpPr/>
          <p:nvPr/>
        </p:nvSpPr>
        <p:spPr>
          <a:xfrm><a:off x="457200" y="228600"/><a:ext cx="8229600" cy="914400"/></a:xfrm>
          <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
          <a:noFill/>
          <a:ln><a:noFill/></a:ln>
        </p:spPr>
        <p:txBody>
          <a:bodyPr/>
          <a:lstStyle/>
          <a:p><a:r><a:rPr b="1" lang="es-NI" sz="3200"/><a:t>$titleEsc</a:t></a:r></a:p>
        </p:txBody>
      </p:sp>
      <p:sp>
        <p:nvSpPr>
          <p:cNvPr id="3" name="Content"/>
          <p:cNvSpPr/>
          <p:nvPr/>
        </p:nvSpPr>
        <p:spPr>
          <a:xfrm><a:off x="685800" y="1257300"/><a:ext cx="7772400" cy="3429000"/></a:xfrm>
          <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
          <a:noFill/>
          <a:ln><a:noFill/></a:ln>
        </p:spPr>
        <p:txBody>
          <a:bodyPr/>
          <a:lstStyle/>
          $($bodyParagraphs.ToString())
        </p:txBody>
      </p:sp>
      $logoShape
    </p:spTree>
  </p:cSld>
  <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
</p:sld>
"@
}

function New-Pptx {
    param(
        [string]$FilePath,
        [array]$Slides,
        [string]$CoverLogoPath = ""
    )

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("pptx_" + [Guid]::NewGuid().ToString("N"))
    Ensure-Dir -Path $tempRoot
    Ensure-Dir -Path (Join-Path $tempRoot "_rels")
    Ensure-Dir -Path (Join-Path $tempRoot "docProps")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt\_rels")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt\slides")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt\slides\_rels")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt\slideLayouts")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt\slideLayouts\_rels")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt\slideMasters")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt\slideMasters\_rels")
    Ensure-Dir -Path (Join-Path $tempRoot "ppt\theme")

    $slideOverrides = ""
    $hasLogo = -not [string]::IsNullOrWhiteSpace($CoverLogoPath) -and (Test-Path -LiteralPath $CoverLogoPath)
    $imageContentType = ""
    if ($hasLogo) {
        $imageContentType = "  <Default Extension=`"png`" ContentType=`"image/png`"/>`n"
        Ensure-Dir -Path (Join-Path $tempRoot "ppt\media")
        Copy-Item -LiteralPath $CoverLogoPath -Destination (Join-Path $tempRoot "ppt\media\logo-trawzacons.png") -Force
    }
    $presentationRels = @()
    $slideIdList = @()
    $relIndex = 2
    $slideId = 256

    for ($i = 1; $i -le $Slides.Count; $i++) {
        $slideOverrides += "  <Override PartName=`"/ppt/slides/slide$i.xml`" ContentType=`"application/vnd.openxmlformats-officedocument.presentationml.slide+xml`"/>`n"
        $presentationRels += "  <Relationship Id=`"rId$relIndex`" Type=`"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide`" Target=`"slides/slide$i.xml`"/>"
        $slideIdList += "    <p:sldId id=`"$slideId`" r:id=`"rId$relIndex`"/>"
        $relIndex++
        $slideId++

        $includeLogo = $hasLogo -and $i -eq 1
        $slideXml = New-SlideXml -Title $Slides[$i - 1].Title -Lines $Slides[$i - 1].Lines -IncludeLogo:$includeLogo
        Write-Utf8File -Path (Join-Path $tempRoot "ppt\slides\slide$i.xml") -Content $slideXml

        $slideRel = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
</Relationships>
"@
        if ($includeLogo) {
            $slideRel = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../media/logo-trawzacons.png"/>
</Relationships>
"@
        }
        Write-Utf8File -Path (Join-Path $tempRoot "ppt\slides\_rels\slide$i.xml.rels") -Content $slideRel
    }

    $ct = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  $imageContentType
  <Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"/>
  <Override PartName="/ppt/slideMasters/slideMaster1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml"/>
  <Override PartName="/ppt/slideLayouts/slideLayout1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml"/>
  <Override PartName="/ppt/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/>
$slideOverrides  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>
"@

    $rootRels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
"@

    $presentationXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:presentation xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:sldMasterIdLst>
    <p:sldMasterId id="2147483648" r:id="rId1"/>
  </p:sldMasterIdLst>
  <p:sldIdLst>
$($slideIdList -join "`n")
  </p:sldIdLst>
  <p:sldSz cx="12192000" cy="6858000" type="screen16x9"/>
  <p:notesSz cx="6858000" cy="9144000"/>
  <p:defaultTextStyle>
    <a:defPPr/>
    <a:lvl1pPr marL="0" indent="0"><a:defRPr sz="1800"/></a:lvl1pPr>
  </p:defaultTextStyle>
</p:presentation>
"@

    $presentationRelsXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="slideMasters/slideMaster1.xml"/>
$($presentationRels -join "`n")
</Relationships>
"@

    $slideMasterXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sldMaster xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
  <p:cSld name="Master">
    <p:bg><p:bgRef idx="1001"><a:schemeClr val="bg1"/></p:bgRef></p:bg>
    <p:spTree>
      <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
      <p:grpSpPr>
        <a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm>
      </p:grpSpPr>
    </p:spTree>
  </p:cSld>
  <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" accent2="accent2" accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6" hlink="hlink" folHlink="folHlink"/>
  <p:sldLayoutIdLst>
    <p:sldLayoutId id="2147483649" r:id="rId1"/>
  </p:sldLayoutIdLst>
  <p:txStyles>
    <p:titleStyle><a:lvl1pPr algn="l"><a:defRPr sz="3200" b="1"/></a:lvl1pPr></p:titleStyle>
    <p:bodyStyle><a:lvl1pPr marL="342900" indent="-285750"><a:defRPr sz="1800"/></a:lvl1pPr></p:bodyStyle>
    <p:otherStyle><a:lvl1pPr><a:defRPr sz="1800"/></a:lvl1pPr></p:otherStyle>
  </p:txStyles>
</p:sldMaster>
"@

    $slideMasterRels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml"/>
</Relationships>
"@

    $slideLayoutXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<p:sldLayout xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" type="blank" preserve="1">
  <p:cSld name="Blank">
    <p:spTree>
      <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
      <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
    </p:spTree>
  </p:cSld>
  <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
</p:sldLayout>
"@

    $slideLayoutRels = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml"/>
</Relationships>
"@

    $themeXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Office Theme">
  <a:themeElements>
    <a:clrScheme name="Office">
      <a:dk1><a:srgbClr val="1F2937"/></a:dk1>
      <a:lt1><a:srgbClr val="FFFFFF"/></a:lt1>
      <a:dk2><a:srgbClr val="111827"/></a:dk2>
      <a:lt2><a:srgbClr val="F3F4F6"/></a:lt2>
      <a:accent1><a:srgbClr val="2563EB"/></a:accent1>
      <a:accent2><a:srgbClr val="059669"/></a:accent2>
      <a:accent3><a:srgbClr val="DC2626"/></a:accent3>
      <a:accent4><a:srgbClr val="0EA5E9"/></a:accent4>
      <a:accent5><a:srgbClr val="D97706"/></a:accent5>
      <a:accent6><a:srgbClr val="7C3AED"/></a:accent6>
      <a:hlink><a:srgbClr val="2563EB"/></a:hlink>
      <a:folHlink><a:srgbClr val="7C3AED"/></a:folHlink>
    </a:clrScheme>
    <a:fontScheme name="Office">
      <a:majorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
      <a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
    </a:fontScheme>
    <a:fmtScheme name="Office">
      <a:fillStyleLst>
        <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
      </a:fillStyleLst>
      <a:lnStyleLst>
        <a:ln w="9525" cap="flat" cmpd="sng" algn="ctr">
          <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
          <a:prstDash val="solid"/>
        </a:ln>
      </a:lnStyleLst>
      <a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>
      <a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst>
    </a:fmtScheme>
  </a:themeElements>
  <a:objectDefaults/>
  <a:extraClrSchemeLst/>
</a:theme>
"@

    $nowIso = (Get-Date).ToString("s") + "Z"
    $core = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>Presentacion de avances IT y Operaciones</dc:title>
  <dc:creator>IT Service Desk</dc:creator>
  <cp:lastModifiedBy>IT Service Desk</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">$nowIso</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">$nowIso</dcterms:modified>
</cp:coreProperties>
"@

    $app = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Microsoft Office PowerPoint</Application>
  <Slides>$($Slides.Count)</Slides>
</Properties>
"@

    Write-Utf8File -Path (Join-Path $tempRoot "[Content_Types].xml") -Content $ct
    Write-Utf8File -Path (Join-Path $tempRoot "_rels\.rels") -Content $rootRels
    Write-Utf8File -Path (Join-Path $tempRoot "ppt\presentation.xml") -Content $presentationXml
    Write-Utf8File -Path (Join-Path $tempRoot "ppt\_rels\presentation.xml.rels") -Content $presentationRelsXml
    Write-Utf8File -Path (Join-Path $tempRoot "ppt\slideMasters\slideMaster1.xml") -Content $slideMasterXml
    Write-Utf8File -Path (Join-Path $tempRoot "ppt\slideMasters\_rels\slideMaster1.xml.rels") -Content $slideMasterRels
    Write-Utf8File -Path (Join-Path $tempRoot "ppt\slideLayouts\slideLayout1.xml") -Content $slideLayoutXml
    Write-Utf8File -Path (Join-Path $tempRoot "ppt\slideLayouts\_rels\slideLayout1.xml.rels") -Content $slideLayoutRels
    Write-Utf8File -Path (Join-Path $tempRoot "ppt\theme\theme1.xml") -Content $themeXml
    Write-Utf8File -Path (Join-Path $tempRoot "docProps\core.xml") -Content $core
    Write-Utf8File -Path (Join-Path $tempRoot "docProps\app.xml") -Content $app

    if (Test-Path -LiteralPath $FilePath) {
        Remove-Item -LiteralPath $FilePath -Force
    }
    $zipPath = [System.IO.Path]::ChangeExtension($FilePath, ".zip")
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $tempRoot "*") -DestinationPath $zipPath -Force
    Move-Item -LiteralPath $zipPath -Destination $FilePath -Force
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

$projectRoot = (Get-Location).Path
$targetDir = Join-Path $projectRoot $OutputDir
Ensure-Dir -Path $targetDir

$today = Get-Date -Format "dd/MM/yyyy HH:mm"

# KPI reales (corte SQL actual)
$kpiTotalTickets = 10
$kpiActiveTickets = 7
$kpiOpenTickets = 6
$kpiInProgressTickets = 1
$kpiClosedTickets = 3
$kpiOverdueTickets = 7
$kpiMttrHours = "29.78"
$kpiSlaRate = "0.0%"
$kpiCurrentMonth = 10
$kpiPreviousMonth = 0
$kpiTopDepartments = "IT (5), Operaciones (2)"

$kpiCredTotal = 0
$kpiCredActive = 0

$kpiOpTotalRegistros = 1584
$kpiOpTotalCargas = 1
$kpiOpToneladas = "48573.08"
$kpiOpGalones = "125503.11"
$kpiOpTopRutas = @(
    "TRITON - POZO BONO (33359.04 t)",
    "PAVON ASM - PAVON NORTE (7876.46 t)",
    "TRITON - TAJO LA TIGRA (7299.94 t)"
)
$kpiOpEstadoResumen = "FINALIZADO (1497), EN RUTA (78), ANULADO (6), ENTRADA (3)"

$wordParagraphs = @(
    "GRUPO TRAWZACONS - DOCUMENTO GERENCIAL",
    "Documento preparado para presentacion de avances de IT y Operaciones.",
    "Fecha de emision: $today",
    "Corte de datos KPI: $today (fuente SQL Server TrawzaconsDB).",
    "",
    "## 1. Objetivo del instructivo",
    "Estandarizar el uso diario de los modulos IT y Operaciones para mejorar tiempos de respuesta, trazabilidad y control operativo.",
    "",
    "## 2. Alcance",
    "Aplica a personal de IT (Administradores y Tecnicos), Operaciones y Gerencia General para seguimiento de indicadores.",
    "",
    "## 3. Instructivo para Usuario IT",
    "Paso 1: Ingresar al sistema con rol autorizado y validar notificaciones pendientes.",
    "Paso 2: Revisar tablero de incidencias: prioridad, estado, tecnico asignado y SLA.",
    "Paso 3: Ejecutar acciones rapidas sobre tickets (editar, ver, PDF, cerrar, eliminar segun permisos).",
    "Paso 4: Usar Calendario IT para planificar mantenimientos, ventanas de cambio y alertas de cumplimiento.",
    "Paso 5: Generar Reportes IT para seguimiento gerencial (SLA, MTTR, tickets por estado/prioridad, vencidos).",
    "Paso 6: Gestionar Boveda de Credenciales con controles de seguridad (mostrar/ocultar, generar clave, copiar, notas tecnicas).",
    "Paso 7: Administrar usuarios (alta, edicion, activacion/inactivacion) sin perder la estructura base de formulario.",
    "",
    "## 4. Instructivo para Usuario de Operaciones",
    "Paso 1: Entrar al modulo Operaciones y revisar alertas criticas del dia.",
    "Paso 2: Consultar seguimiento de indicadores operativos (toneladas, combustible, estado de rutas y cargas).",
    "Paso 3: Registrar y actualizar documentos/controles operativos con trazabilidad.",
    "Paso 4: Emitir reportes PDF para jefaturas y gerencia con corte diario/semanal.",
    "Paso 5: Escalar incidencias a IT cuando exista bloqueo tecnico o riesgo de SLA.",
    "",
    "## 5. KPI reales del sistema (corte actual)",
    "IT - Tickets totales: $kpiTotalTickets",
    "IT - Tickets activos: $kpiActiveTickets | Abiertos: $kpiOpenTickets | En progreso: $kpiInProgressTickets | Cerrados: $kpiClosedTickets",
    "IT - Tickets vencidos (SLA): $kpiOverdueTickets | SLA en ventana segura: $kpiSlaRate | MTTR: $kpiMttrHours h",
    "IT - Tickets creados mes actual: $kpiCurrentMonth | mes anterior: $kpiPreviousMonth",
    "IT - Departamentos con mayor carga activa: $kpiTopDepartments",
    "Boveda de credenciales - Total: $kpiCredTotal | Activas: $kpiCredActive",
    "Operaciones - Registros de seguimiento: $kpiOpTotalRegistros | Cargas operativas: $kpiOpTotalCargas",
    "Operaciones - Toneladas acumuladas: $kpiOpToneladas | Combustible total (gal): $kpiOpGalones",
    "Operaciones - Estado de viajes: $kpiOpEstadoResumen",
    "Operaciones - Top rutas por toneladas: $($kpiOpTopRutas -join '; ')",
    "",
    "## 6. Avances implementados",
    "1) Dashboard y listado de tickets mejorados con enfoque en visibilidad de SLA y acciones rapidas.",
    "2) Ajustes de columnas y experiencia de tabla para evitar cortes de botones y datos.",
    "3) Calendario IT reforzado para planificacion y seguimiento.",
    "4) Reportes IT ampliados para presentacion ejecutiva y control de cumplimiento.",
    "5) Listado/creacion de usuarios mejorado manteniendo la base del formulario existente.",
    "6) Boveda de credenciales mejorada en seguridad operativa (sin romper campos base).",
    "",
    "## 7. Reglas de gobierno y seguridad",
    "Aplicar principio de minimo privilegio para accesos sensibles.",
    "Evitar compartir secretos por chat o correo sin cifrado.",
    "Registrar toda accion critica en auditoria del sistema.",
    "Definir rotacion de credenciales y revisiones periodicas.",
    "",
    "## 8. Proximos pasos",
    "Aprobar fase de automatizacion de alertas y reportes programados.",
    "Definir politicas de rotacion para credenciales criticas.",
    "Consolidar tablero ejecutivo mensual IT + Operaciones."
)

$slides = @(
    @{
        Title = "GRUPO TRAWZACONS - Avances IT y Operaciones"
        Lines = @(
            "Presentacion ejecutiva para Gerencia",
            "Fecha: $today",
            "Corte KPI real: SQL Server TrawzaconsDB"
        )
    },
    @{
        Title = "Resumen ejecutivo de impacto"
        Lines = @(
            "Objetivo: elevar control operativo y cumplimiento de SLA",
            "Resultado: mejor visibilidad de incidencias y trazabilidad de operaciones",
            "Enfoque: mejoras sin romper formularios base"
        )
    },
    @{
        Title = "KPI IT (corte real)"
        Lines = @(
            "Tickets totales: $kpiTotalTickets | Activos: $kpiActiveTickets | Vencidos: $kpiOverdueTickets",
            "Abiertos: $kpiOpenTickets | En progreso: $kpiInProgressTickets | Cerrados: $kpiClosedTickets",
            "MTTR: $kpiMttrHours h | SLA en ventana segura: $kpiSlaRate",
            "Creacion mensual: actual $kpiCurrentMonth vs anterior $kpiPreviousMonth",
            "Mayor carga activa: $kpiTopDepartments"
        )
    },
    @{
        Title = "KPI Operaciones (corte real)"
        Lines = @(
            "Registros de seguimiento: $kpiOpTotalRegistros | Cargas: $kpiOpTotalCargas",
            "Toneladas acumuladas: $kpiOpToneladas",
            "Combustible acumulado (gal): $kpiOpGalones",
            "Estado viajes: $kpiOpEstadoResumen",
            "Top rutas: $($kpiOpTopRutas -join '; ')"
        )
    },
    @{
        Title = "Avances funcionales entregados"
        Lines = @(
            "Tablero de incidencias y acciones rapidas optimizadas",
            "Calendario IT y reportes gerenciales fortalecidos",
            "Boveda de credenciales con controles de seguridad operativa",
            "Formulario de usuarios mejorado sin perder base",
            "Mejor experiencia de tabla y visualizacion para usuarios administradores"
        )
    },
    @{
        Title = "Riesgos y mitigacion"
        Lines = @(
            "Riesgo: baja adopcion de buenas practicas",
            "Mitigacion: instructivo, capacitacion y seguimiento semanal",
            "Riesgo: uso inseguro de credenciales",
            "Mitigacion: controles de visualizacion, copia y rotacion"
        )
    },
    @{
        Title = "Proxima fase recomendada"
        Lines = @(
            "Automatizar alertas inteligentes por SLA y backlog",
            "Programar reportes periodicos para Gerencia",
            "Tablero ejecutivo consolidado IT + Operaciones",
            "Politica formal de auditoria y rotacion de credenciales"
        )
    }
)

$slidesExecutive = @(
    @{
        Title = "Resumen Ejecutivo - IT y Operaciones"
        Lines = @(
            "Grupo Trawzacons",
            "Fecha: $today",
            "Corte KPI real: SQL Server TrawzaconsDB"
        )
    },
    @{
        Title = "KPI clave IT"
        Lines = @(
            "Tickets activos: $kpiActiveTickets de $kpiTotalTickets",
            "Vencidos por SLA: $kpiOverdueTickets",
            "MTTR: $kpiMttrHours h | SLA en ventana segura: $kpiSlaRate",
            "Mayor carga activa: $kpiTopDepartments"
        )
    },
    @{
        Title = "KPI clave Operaciones"
        Lines = @(
            "Registros de seguimiento: $kpiOpTotalRegistros",
            "Toneladas acumuladas: $kpiOpToneladas",
            "Combustible acumulado (gal): $kpiOpGalones",
            "Estado predominante: FINALIZADO"
        )
    },
    @{
        Title = "Avances implementados"
        Lines = @(
            "Mejoras en dashboard, calendario, reportes y tablas",
            "Boveda de credenciales reforzada con controles de seguridad",
            "Formularios mejorados sin romper estructura base"
        )
    },
    @{
        Title = "Decision solicitada a Gerencia"
        Lines = @(
            "Aprobar fase de automatizacion de alertas y reportes",
            "Definir politica de seguridad y rotacion de credenciales",
            "Validar tablero ejecutivo mensual IT + Operaciones"
        )
    }
)

$docxPath = Join-Path $targetDir "Instructivo_IT_Operaciones_Gerencia.docx"
$pptxPath = Join-Path $targetDir "Presentacion_Avances_IT_Operaciones_Gerencia.pptx"
$pptxExecPath = Join-Path $targetDir "Resumen_Ejecutivo_5_Diapositivas_Gerencia.pptx"

$resolvedLogoPath = if ([System.IO.Path]::IsPathRooted($LogoPath)) { $LogoPath } else { Join-Path $projectRoot $LogoPath }

New-Docx -FilePath $docxPath -Paragraphs $wordParagraphs -Title "Instructivo de Uso - IT y Operaciones"
New-Pptx -FilePath $pptxPath -Slides $slides -CoverLogoPath $resolvedLogoPath
New-Pptx -FilePath $pptxExecPath -Slides $slidesExecutive -CoverLogoPath $resolvedLogoPath

Write-Output "DOCX: $docxPath"
Write-Output "PPTX: $pptxPath"
Write-Output "PPTX_RESUMEN_5: $pptxExecPath"

