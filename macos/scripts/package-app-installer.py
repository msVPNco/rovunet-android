"""Build a standard, payload-only app Installer using pinned macospkg 0.7.3.

Tool: deploymenttheory/go-macos-pkg 16a70452f39fc133551003ea5c40ece2b1467841
Windows release archive SHA256:
46a40db8f91302b002746340ed29f79cb1b4ea189faaf1ad61c7b7a785cea249
No app/TUN service is installed or run on the build host.
"""
import argparse,json,pathlib,re,subprocess,xml.etree.ElementTree as ET

ARCHITECTURES={'osx-arm64':('arm64','Apple Silicon'),'osx-x64':('x86_64','Intel x64')}

def distribution(template,rid):
    architecture,label=ARCHITECTURES[rid]
    root=ET.fromstring(template)
    root.find('title').text='Rovunet 0.1.6 Beta — '+label
    root.find('options').set('hostArchitectures',architecture)
    return ET.tostring(root,encoding='utf-8',xml_declaration=True)

def main():
    p=argparse.ArgumentParser();p.add_argument('app',type=pathlib.Path);p.add_argument('output',type=pathlib.Path);p.add_argument('--tool',required=True,type=pathlib.Path);a=p.parse_args()
    assert a.app.name=='Rovunet.app' and a.app.is_dir()
    assert not a.output.exists(),'Refusing to overwrite an existing candidate'
    info=json.loads((a.app/'Contents/Resources/BUILD-INFO.json').read_text(encoding='utf-8-sig'))
    assert info['rid'] in ARCHITECTURES and info['version']=='0.1.6-beta' and info['developmentAdhocSigned']
    architecture,label=ARCHITECTURES[info['rid']]
    a.output.parent.mkdir(parents=True,exist_ok=True)
    component=a.output.parent/'Rovunet-App.pkg'
    assert not component.exists(),'Use a fresh output folder'
    args=[str(a.tool),'build',str(a.app.parent),str(component),'--identifier','org.rovunet.macos.development.pkg','--version','0.1.6.0','--install-location','/Applications','--ownership','recommended','--no-bundle-relocation','--compression','gzip','--min-os-version','15.0','--auth','root','--source-date-epoch','1794355200','--filter','a^']
    native=[]
    for file in sorted(a.app.rglob('*')):
        if not file.is_file():continue
        with file.open('rb') as stream:magic=stream.read(4)
        if magic in (bytes.fromhex('cffaedfe'),bytes.fromhex('cafebabe'),bytes.fromhex('cafebabf')):
            rel='./Rovunet.app/'+file.relative_to(a.app).as_posix();native.append(rel);args+=['--executable','^'+re.escape(rel)+'$']
    assert len(native)>=30,'Missing executable mode inventory'
    subprocess.run(args,check=True)
    packaging=pathlib.Path(__file__).resolve().parent.parent/'packaging/app-installer'
    target_distribution=a.output.parent/('Distribution-'+info['rid']+'.xml')
    target_distribution.write_bytes(distribution((packaging/'Distribution.xml').read_bytes(),info['rid']))
    subprocess.run([str(a.tool),'product',str(a.output),'--package',str(component),'--distribution',str(target_distribution),'--resources',str(packaging/'resources'),'--source-date-epoch','1794355200'],check=True)
    print(json.dumps({'file':a.output.name,'version':info['version'],'architecture':architecture,'label':label,'installLocation':'/Applications','executableFiles':native,'scripts':False,'developerIdInstallerSigned':False,'appleInstallerValidated':False}))
if __name__=='__main__':main()
