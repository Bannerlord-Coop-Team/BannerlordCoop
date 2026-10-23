import json, pathlib, re, hashlib, struct, datetime, subprocess

d=pathlib.Path(__file__).resolve().parent
def read(name):
    return json.loads((d/name).read_text(encoding='utf-8-sig'))
def output(name):
    x=read(name)
    assert x['ok'], name
    assert x['process']['runToken']=='issue3783-20261023T114021',name
    assert x['result'].get('succeeded',True),name
    return x['result']['output']
def fields(text):
    return dict(re.findall(r'([\w.]+)=([^\s]+)',text))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

branches=[]
for key,enum,stage,title,reward,xp,relation,old,renown in [
    ('ra','SuccessByAgreement','agreement',"Tuvul's Revolt",5400,-10,8,3,1),
    ('rc','SuccessByBattle','won',"Olorun's Revolt",5250,30,9,4,5.047),
    ('rb','FailureByRelease','released',"Huskur's Revolt",0,0,-5,5,0),
]:
    roles={}
    for role in ['server','testclient','testclient2']:
        check=output(f'{key}-{stage}-try1-{role}.json')
        assert check.startswith(f'CHECK_OK {key} {stage}'),(key,role)
        if role=='testclient2':
            assert 'branchLineOnOwner=True endingCountOnOwner=1' in check,(key,role)
        before=fields(output(f'{key}-arm-{role}.json'))
        after=fields(output(f'{key}-after-counters-{role}.json'))
        stopped=fields(output(f'{key}-counters-stopped-{role}.json'))
        for f in ['issue','giver','party','handle']: assert before[f]==after[f]==stopped[f],(key,role,f)
        for f in ['active','registeredById','registeredByHandle','inWorld']:
            assert before[f]=='True' and after[f]=='False' and stopped[f]=='False',(key,role,f)
        for f,v in after.items():
            if f.startswith(('sent.','received.','local.','native.')):
                assert before[f]=='0' and stopped[f]==v,(key,role,f)
        for name in ['NetworkApplyDestroyParty','NetworkDestroyInstance','NetworkIssueFinalized']:
            assert after['sent.'+name]==('1' if role=='server' else '0'),(key,role,name)
            assert after['received.'+name]==('0' if role=='server' else '1'),(key,role,name)
        assert after['native.MobilePartyDestroyed']=='1',(key,role)
        for name in ['DestroyPartyApplied','InstanceDestroyed']:
            assert after['local.'+name]==('1' if role=='server' else '0'),(key,role,name)
        assert after['sent.NetworkOwnerBattleEnded']==('1' if key=='rc' and role=='server' else '0')
        assert after['received.NetworkOwnerBattleEnded']==('1' if key=='rc' and role=='testclient2' else '0')
        a=output(f'{key}-before-rural-{role}.json'); b=output(f'{key}-after-rural-{role}.json')
        expected={'ra':0,'rc':-2,'rb':1}[key]
        assert a.splitlines()[0]==b.splitlines()[0] and 'occupation=RuralNotable village=' in a,(key,role)
        def relations(t):return {c:int(v) for c,v in re.findall(r'player=(\S+) hero=\S+ relation=(-?\d+)',t)}
        av,bv=relations(a),relations(b)
        assert bv['testclient']==av['testclient'] and bv['testclient2']-av['testclient2']==expected,(key,role,av,bv)
        roles[role]={'identity':{f:after[f] for f in ['issue','giver','party','handle']},'counters':{f:int(v) for f,v in after.items() if f.startswith(('sent.','received.','local.','native.'))},'ruralSample':a.splitlines()[0],'ruralBefore':av,'ruralAfter':bv}
    state=output(f'{key}-after-state-server.json')
    owner=fields(next(x for x in state.splitlines() if x.startswith('player=testclient2 ')))
    observer=fields(next(x for x in state.splitlines() if x.startswith('player=testclient ')))
    assert int(owner['goldDelta'])==reward and int(owner['honorXpDelta'])==xp and int(owner['giverRelationDelta'])==relation,(key,owner)
    assert abs(float(owner['renownDelta'])-renown)<0.001,(key,owner)
    assert all(observer[f]=='0' for f in ['goldDelta','honorXpDelta','giverRelationDelta','renownDelta']),(key,observer)
    native=output(f'{key}-{enum}-journal-select-testclient2.json')
    assert f"Native journal selected '{title}'; stages=2; active=0 old={old}." in native
    no=read(f'{key}-{enum}-journal-no-matching-entry-testclient.json')
    assert no['ok'] and no['result']['succeeded']==False and no['result']['output']==f'No native journal entry matches {title}'
    for role in ['testclient','testclient2']:
        s=read(f'{key}-{enum}-journal-closed-testclient-status-{role}.json')['result']
        assert s['activeState'].endswith('.MapState') and s['topScreen'].endswith('.MapScreen')
        assert 'active=False' in output(f'{key}-{enum}-journal-closed-testclient-conv-{role}.json')
    branches.append({'branch':enum,'slot':key,'status':'DONE','title':title,'owner':'B/testclient2','observer':'A/testclient','ownerDelta':owner,'observerDelta':observer,'nativeJournal':native,'peers':roles})

assert read('kit-compile.json')['exitCode']==0 and read('kit-compile.json')['errors']==0 and read('kit-compile.json')['kitErrors']==[]
cleanup=read('cleanup-issue3783-20261023T114021/cleanup.json')
assert cleanup['verifiedAbsent'] and cleanup['remainingProcessIds']==[]
assert all(x['equal'] for x in read('config-seed-final.json'))
assert read('final-inventory.json')['remainingProcessIds']==[] and read('final-inventory.json')['benchLockAbsent']
assert len(read('dependency-18-product-blobs.json'))==18 and all(x['equal'] for x in read('dependency-18-product-blobs.json'))
assert read('final-inventory.json')['head']=='bd74a2d5323edd542faebc14b4b9802e6db81184' and not read('final-inventory.json')['status']
captions={}
for b in branches:
    for role,pid in [('testclient2',50816),('testclient',56212)]:
        name=f"{b['slot']}-{b['branch']}-journal-{role}-{pid}.png"
        text=(f"{b['branch']}: B's native Old Quests entry for {b['title']}, opening and full ending once. " + ('Quest reward is shown once.' if b['branch']!='FailureByRelease' else 'Failed by the actual release dialogue, no quest reward.')) if role=='testclient2' else f"{b['branch']}: observer A has no matching {b['title']} entry, active=0 and only its two seed old quests."
        captions[name]=text
shots=[]
logs='\n'.join(p.read_text(encoding='utf-8-sig',errors='replace') for p in d.glob('*.log'))
for p in sorted((d/'screenshots').glob('*.png')):
    w,h=struct.unpack('>II',p.read_bytes()[16:24])
    match=re.search(re.escape(p.name)+r' sha256=(\w+) captured=([^\s]+)',logs)
    if match:assert match.group(1)==sha(p),p.name
    shots.append({'path':p.relative_to(d).as_posix(),'sha256':sha(p),'width':w,'height':h,'runToken':'issue3783-20261023T114021','clientPid':int(re.search(r'-(\d+)\.png$',p.name).group(1)),'capturedUtc':match.group(2) if match else None,'caption':captions.get(p.name),'visuallyInspected':p.name in captions or p.name.startswith(('baseline-both','final-both-map','rb-before-journal','rc-before-journal-owner','ra-before-journal'))})
assert all(x['capturedUtc'] for x in shots if x['caption'])
result={'status':'DONE','head':'bd74a2d5323edd542faebc14b4b9802e6db81184','tree':'311491b1b93a9b28a6edf83fc2dab4fdb5981cd6','runToken':'issue3783-20261023T114021','roles':{'serverEngine':60368,'serverWrapper':71700,'AObserver':56212,'BOwner':50816},'branches':branches,'screenshots':shots,'cleanup':cleanup,'caveats':['Battle total renown includes native battle rewards; strict current kit checks >=3 quest branch minimum, observed total 5.047.','Counter observations are logical transport sends/broker receives and native destruction callbacks, not datagram counts.','Paged journal text reads could go stale; final captures use successful fresh first-page inspections plus visually inspected full PNGs.','No clean-log claim; retained runtime logs contain managed diagnostics.','No new R4, CI, PR promotion or source mutation was performed.']}
(d/'RESULT.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('AUDIT_PASS 3 branches, 9 strict peer checks, 9 scoped counter sets, 9 real rural samples, 6 inspected ending journals, cleanup/config/source checks')
