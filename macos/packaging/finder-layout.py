"""Create Finder icon placement. Requires ds_store==1.3.3, mac_alias==2.2.3."""
import argparse, datetime, pathlib
from ds_store import DSStore
from mac_alias import Alias, VolumeInfo, TargetInfo

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=pathlib.Path)
parser.add_argument('--volume', default='Rovunet')
args = parser.parse_args()
args.directory.mkdir(parents=True, exist_ok=True)
created = datetime.datetime(2026,11,11,tzinfo=datetime.timezone.utc)
# The HFS writer assigns depth-first IDs in byte-sorted name order:
# .DS_Store=16, .background=17, background.png=18.
volume = VolumeInfo(args.volume, created, b'H+', 5, 0, b'\0\0', posix_path='/Volumes/'+args.volume)
target = TargetInfo(0,'background.png',17,18,created,b'\0'*4,b'PNGf',folder_name='.background',cnid_path=[2,17,18],carbon_path=args.volume+':.background:background.png',posix_path='/.background/background.png')
alias = Alias(volume=volume,target=target).to_bytes()
with DSStore.open(str(args.directory/'.DS_Store'),'w+') as db:
    db['Rovunet.app']['Iloc'] = (170,210)
    db['Applications']['Iloc'] = (470,210)
    db['.']['bwsp'] = {'ShowStatusBar':False,'ShowToolbar':False,'ShowTabView':False,'ShowPathbar':False,'ShowSidebar':False,'WindowBounds':'{{140, 160}, {640, 420}}'}
    db['.']['icvp'] = {'viewOptionsVersion':1,'backgroundType':2,'backgroundImageAlias':alias,'iconSize':128.0,'gridSpacing':160.0,'gridOffsetX':0.0,'gridOffsetY':0.0,'textSize':15.0,'labelOnBottom':True,'showItemInfo':False,'showIconPreview':False,'arrangeBy':'none'}
    # icvp alone does not select icon view. Finder may otherwise reopen the
    # volume in the user's list/column view, with tiny icons over the backdrop.
    # The same icvl record is written by dmgbuild's canonical implementation.
    db['.']['icvl'] = ('type',b'icnv')
    db['.']['vSrn'] = ('long',1)
print('Created Finder layout with Applications link and two icon positions.')
