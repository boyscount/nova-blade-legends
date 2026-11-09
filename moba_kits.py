# power budget: damage in P per skill, divided by cooldown -> per 10 s; utility converted to P
# stun/knockup 1s=0.8P, root 1s=0.6P, slow: pct*sec*0.67P, shield/heal 10% maxhp=0.5P (x allies), dash/blink=0.3P, invis/untargetable 1s=0.4P, dmg cut 10% 1s=0.05P, buff atk 10% 1s on team=0.04P
def U(stun=0,root=0,slow=0,slows=0,shield=0,dash=0,invis=0,cut=0,cuts=0,buff=0,buffs=0,allies=1):
    return stun*.8+root*.6+slow*slows*.67+shield*5*allies+dash*.3+invis*.4+cut*cuts*.5+buff*buffs*.4*allies
K={
"warrior":[
 ("วัลก้า",[(2.1,7,U()),(1.4,10,U(stun=.6,dash=1)),(0,14,U(slow=.3,slows=2,buff=.2,buffs=5,allies=2))]),
 ("เลออน",[(1.3,9,U(stun=.8,dash=1)),(1.8,7,U(slow=.3,slows=1.5)),(0,16,U(shield=.15)+U(shield=.08,allies=2))]),
 ("ใหม่: อัศวินโซ่",[(1.2,8,U(root=.5)),(2.2,8,U()),(1.2,14,U(stun=1.0))]),],
"assassin":[
 ("ซากุระ",[(1.6,6,U(dash=1)),(1.6,8,U()),(2.0,12,U(invis=.6))]),
 ("คาเงะ",[(1.8,5,U()),(1.2,9,U(dash=1)),(1.4,13,U(invis=1.5))]),
 ("เรเวน",[(1.4,7,U(dash=1)),(2.0,9,U(slow=.2,slows=2)),(2.4,14,U())]),],
"archer":[
 ("แอริส",[(1.8,5,U()),(1.6,10,U(dash=1)),(2.4,11,U(stun=.3))]),
 ("เอลวีน",[(2.25,6,U()),(2.6,9,U()),(0,10,U(dash=1)+1.0)]),
 ("โรวัน",[(1.0,8,U(root=1.2)),(2.9,7,U()),(1.2,12,U()+ .15*3)]),],
"mage":[
 ("อัลดริค",[(2.4,7,U()),(2.8,10,U()),(0,14,U(shield=.25,slow=.4,slows=2))]),
 ("ไนต์",[(1.8,6,U()),(2.4,10,U(slow=.2,slows=4)),(1.5,12,U(shield=.12))]),
 ("เอเรีย",[(1.8,7,U(stun=.3)),(1.0,10,U(root=1.2)),(3.0,13,U(slow=.15,slows=3))]),],
"cleric":[
 ("โซเลีย",[(0,12,U(shield=.18,allies=2)),(1.5,16,U(cut=.2,cuts=5,allies=2)),(1.8,9,U(stun=.6))]),
 ("ไลร่า",[(0,8,U(shield=.22)),(2.2,10,U(slow=.3,slows=1.5)),(0,15,U(buff=.3,buffs=3,allies=3)+.6)]),
 ("ใหม่: นักบวชไฟ",[(2.0,8,U()),(0,12,U(shield=.15,allies=2)),(1.2,14,U(stun=.8))]),],
"monk":[
 ("หมิง",[(1.7,7,U(dash=1,stun=.3)),(2.0,9,U()),(1.6,13,U(shield=.12,stun=.3))]),
 ("เทนซิน",[(1.6,8,U(stun=.7)),(2.6,10,U(stun=.6)),(0,14,U(cut=.4,cuts=3,stun=1))]),
 ("ใหม่: นักมวยเสือ",[(1.6,6,U(dash=1)),(2.2,9,U()),(1.2,12,U(slow=.3,slows=2,buff=.3,buffs=4))]),],
}
for c,hs in K.items():
    rows=[]
    for n,sk in hs:
        dmg=sum(d for d,cd,u in sk); per10=sum(d/cd*10 for d,cd,u in sk); util=sum(u for d,cd,u in sk); tot=sum((d+u)/cd*10 for d,cd,u in sk)
        rows.append((n,dmg,per10,util,tot))
    avg=sum(r[4] for r in rows)/3
    for n,d,p,u,t in rows: print(f"{c:9s} {n:16s} combo {d:4.1f}P  dmg/10s {p:4.1f}P  util {u:4.2f}P  score {t:4.2f}  ({(t/avg-1)*100:+.0f}%)")
