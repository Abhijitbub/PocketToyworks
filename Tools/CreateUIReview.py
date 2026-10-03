"""Create review sheets and native-pixel detail crops from verified captures."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
version = '0.5.0'
shots = root / f'Verification/Screenshots/v{version}'
screens = ['game', 'tutorial', 'home', 'level', 'profile', 'frames', 'settings', 'workshop', 'lives', 'collection']
for resolution in ['360x640', '360x720', '360x785', '480x800', '1080x1920', '1080x2160', '1272x2772']:
    sheet = Image.new('RGB', (1500, 1400), '#eef3ff')
    draw = ImageDraw.Draw(sheet)
    for index, name in enumerate(screens):
        source = Image.open(shots / f'layout-{name}-{resolution}-safe.png').convert('RGB')
        if source.size != tuple(map(int, resolution.split('x'))):
            raise ValueError(f'Wrong native capture size: {source.size}')
        source.thumbnail((290, 665), Image.Resampling.LANCZOS)
        x, y = (index % 5) * 300, (index // 5) * 700
        sheet.paste(source, (x + (300 - source.width) // 2, y + 25))
        draw.text((x + 8, y + 5), f'{name} | {resolution}', fill='#14224f')
    sheet.save(root / f'Verification/UI-review-{resolution}-{version}.jpg', quality=94)
phone = Image.open(shots / 'layout-game-1272x2772-safe.png')
phone.crop((0, phone.height - 800, phone.width, phone.height)).save(root / f'Verification/UI-detail-{version}.png')
phone.crop((0, 0, phone.width, 425)).save(root / f'Verification/UI-header-detail-{version}.png')
empty = Image.open(shots / 'layout-tutorial-1272x2772-safe.png')
empty.crop((0, empty.height - 800, empty.width, empty.height)).save(root / f'Verification/UI-empty-lane-detail-{version}.png')
flows = ['queue', 'toy-result', 'timed-gameplay', 'leave-confirmation', 'time-up', 'profile-long-name']
sheet = Image.new('RGB', (1800, 720), '#eef3ff')
draw = ImageDraw.Draw(sheet)
for index, name in enumerate(flows):
    source = Image.open(shots / f'{name}-720x1280.png').convert('RGB')
    source.thumbnail((290, 680), Image.Resampling.LANCZOS)
    sheet.paste(source, (index * 300, 25))
    draw.text((index * 300 + 5, 5), name, fill='#14224f')
sheet.save(root / f'Verification/UI-flow-review-{version}.jpg', quality=94)
print('Created seven review sheets and native-pixel filled/empty lane and header detail crops')
flows = ['default-timed', 'paused-home', 'restart-after-undo', 'zero-lives', 'refilled-timed', 'timed-rules-help']
sheet = Image.new('RGB', (1800, 720), '#eef3ff')
draw = ImageDraw.Draw(sheet)
for index, name in enumerate(flows):
    source = Image.open(shots / f'{name}-720x1280.png').convert('RGB')
    source.thumbnail((290, 680), Image.Resampling.LANCZOS)
    sheet.paste(source, (index * 300, 25))
    draw.text((index * 300 + 5, 5), name, fill='#14224f')
sheet.save(root / f'Verification/UI-mode-review-{version}.jpg', quality=94)
