$out=Join-Path $PSScriptRoot '../Documentation/Mockups'
New-Item -ItemType Directory -Force $out | Out-Null
function Box($x,$y,$w,$h,$fill,$r=16) { '<rect x="'+$x+'" y="'+$y+'" width="'+$w+'" height="'+$h+'" rx="'+$r+'" fill="'+$fill+'"/>' }
function Text($x,$y,$s,$size=22,$fill='#20333D') { '<text x="'+$x+'" y="'+$y+'" font-family="Arial,sans-serif" font-size="'+$size+'" fill="'+$fill+'">'+$s+'</text>' }
function Robot($x,$y,$scale=1) {
  '<g transform="translate('+$x+','+$y+') scale('+$scale+')">'+(Box -68 0 136 115 '#237C83')+(Box -82 -95 164 95 '#F27462')+(Box -64 -77 128 54 '#FFF5DE')+(Box -41 -62 13 20 '#20333D' 5)+(Box 28 -62 13 20 '#20333D' 5)+(Box -12 -39 24 5 '#20333D' 2)+(Box -22 27 44 44 '#F2C94C')+(Box -105 10 34 85 '#F2C94C')+(Box 71 10 34 85 '#F2C94C')+(Box -64 113 51 45 '#F27462')+(Box 13 113 51 45 '#F27462')+'</g>'
}
$colors=@('#F27462','#F2C94C','#4D91D9','#9A78C8')
$level=Get-Content (Join-Path $PSScriptRoot '../Assets/PocketToyworks/Resources/Levels/level-06.json') -Raw | ConvertFrom-Json
function PieceShape($id,$cx,$cy,$cell) {
  $p=$level.pieces | Where-Object id -eq $id
  $w=(($p.cells.x | Measure-Object -Maximum).Maximum+1)*$cell
  $h=(($p.cells.y | Measure-Object -Maximum).Maximum+1)*$cell
  $parts='';foreach($c in $p.cells){$parts+=Box ($cx-$w/2+$c.x*$cell) ($cy-$h/2+$c.y*$cell) ($cell-2) ($cell-2) $colors[$p.color] 6};$parts
}
foreach($name in @('gameplay','reveal','collection')) {
  $s='<svg xmlns="http://www.w3.org/2000/svg" width="720" height="1280" viewBox="0 0 720 1280">'+(Box 0 0 720 1280 '#F5EFE3' 0)+(Text 36 48 'POCKET TOYWORKS' 20 '#237C83')+(Text 36 1238 'CONCEPT MOCKUP • NOT A RUNNING GAME SCREENSHOT' 17 '#237C83')
  if($name -eq 'gameplay') {
    $s+=(Text 36 103 'Room for two' 36)+(Text 36 155 'ORDER 06 / 10' 20)+(Text 445 155 '0 / 2 TOYS MADE' 18)+(Text 36 218 'THE FITTING BENCH' 18 '#237C83')
    for($b=0;$b -lt 2;$b++) {
      $s+=Box (32+$b*340) 247 318 316 '#237C83' 24
      for($y=0;$y -lt 4;$y++){for($x=0;$x -lt 4;$x++){$fill=if(($level.moulds[$b].mask -band (1 -shl ($y*4+$x))) -eq 0){'#2A6F73'}else{'#D4DECE'};$s+=Box (44+$b*340+$x*73) (258+$y*73) 67 67 $fill 10}}
    }
    $s+=(Text 125 622 'Park a part. Make room for the next.' 23)+(Text 36 697 'RESERVE' 18 '#237C83')+(Box 36 723 316 99 '#E5E6D7')+(Box 368 723 316 99 '#E5E6D7')+(PieceShape $level.lanes[0].pieces[0] 120 772 31)+(Text 203 780 'SAVED 1' 18)+(Text 426 780 '+   Park one piece' 20)+(Text 36 878 'PARTS' 18 '#237C83')
    for($a=0;$a -lt 3;$a++){
      $px=36+$a*223;$head=if($a -eq 0){1}else{0};$q=$level.lanes[$a].pieces
      $s+=(Box $px 899 202 248 '#E9E2D3')+(Text ($px+62) 928 ('LANE '+[char](65+$a)) 15)+(Box ($px+9) 942 184 103 '#FFFBF2')+(PieceShape $q[$head] ($px+101) 991 29)
      for($j=1;$j -le 2;$j++){if($head+$j -lt $q.Count){$s+=PieceShape $q[$head+$j] ($px+55+($j-1)*90) 1070 14}}
      $s+=Text ($px+56) 1130 (($q.Count-$head).ToString()+' left · View') 16
    }
    $s+=(Text 101 1199 'Undo' 23)+(Text 319 1199 'Rotate' 23)+(Text 556 1199 'Hint' 23)
  } elseif($name -eq 'reveal') {
    $s+=(Text 250 243 'ORDER COMPLETE' 20 '#237C83')+(Text 141 318 'A little toy, made by you.' 34)+(Box 120 768 480 30 '#E2DECE' 15)+(Robot 360 537 1.7)+(Text 286 894 'Meet Pip' 38)+(Text 172 951 'Every part found its place.' 28)+(Box 105 1017 510 75 '#237C83')+(Text 301 1066 'Continue' 26 '#FFFFFF')
  } else {
    $s+=(Text 36 112 'Your little collection' 36)+(Text 36 160 'A shelf full of things you made.' 23)+(Robot 214 420 1.1)+(Box 52 608 616 34 '#CBAE84')+(Text 158 687 'PIP · ROBOT' 20 '#237C83')+(Box 440 378 119 150 '#DBDDCE' 32)+(Text 414 687 'Next: level 6' 20)
    $s+=(Box 52 1020 616 34 '#CBAE84')+(Box 130 825 126 169 '#DBDDCE' 30)+(Box 439 825 126 169 '#DBDDCE' 30)+(Text 114 1128 'Shelf and palettes arrive in a later milestone.' 22)
  }
  $s+='</svg>';[IO.File]::WriteAllText((Join-Path $out ($name+'.svg')),$s)
}
