




procedure bruto_obracun()
local vred_boda, prazno, pr_drzavni, pr_nocni, pr_preko, pr_minul
local f_casova, m_rad, pos_porez,priv_neto,p,q,invumanjenje
local zar_dop, bol_dop,gar_zar,masa, akmasa
local pren_bruto_zar[20]
local pren_bol_bruto[20]
local pren_nak_bruto[20]
local fcasova, casova_za_ob, pr_plac,pr_nedel,pr_pl_za, pr_inval
local pren_inv[20]
local pren_bolbruto[20]
local pren_n_do[20]
local pren_n_pr[20]
local pren_por[20]
local pr_bolov,p_zar_dop,i,neto_deo,priv_min_rad,prva_stopa,druga_stopa
local lin_porez3,ak_porez2, pros_bruto
local razred_iz[10]
local razred_PIOiz[10]
local dop_PIO
// BB 10
local vrbod
local prvi
local sumum1,sumum2

private zar_dop1,zar_dop2, zar_dop3
private zar_dop4,zar_dop5, zar_dop6,zar_dop7,zar_dop8, zar_dop9
private bol_dop1,bol_dop2, bol_dop3, bol_dop4, bol_dop5, bol_dop6,;
        bol_dop7, bol_dop8, bol_dop9
private priv1,priv2,priv3,priv4,priv5,priv6,priv7,priv8,priv9,priv10
private priv11,priv12,priv13,priv14,priv15,priv16,priv17,priv18,priv19,priv20
private priv21,priv22,priv23,priv24,priv25,priv26,priv27,priv28,priv29,priv30
private priv31,priv32,priv33,priv34,priv35,priv36,priv37
private priv40,priv41,priv42,priv43,priv44,priv45,priv46,priv47,priv48
// BB bol
private priv49
//

private p_zar_dop1,p_zar_dop2, p_zar_dop3, p_zar_dop4,p_zar_dop5
private p_zar_dop6,p_zar_dop7,p_zar_dop8, p_zar_dop9
private p_bol_dop1,p_bol_dop2, p_bol_dop3, p_bol_dop4
private p_bol_dop5, p_bol_dop6,p_bol_dop7, p_bol_dop8, p_bol_dop9
private por_dop1,por_dop2,por_dop3,por_dop4,por_dop5,por_dop6,por_dop7,por_dop8
private por_dop9

private p_z_d1[10],p_z_d2[10],p_z_d3[10],p_z_d4[10],p_z_d5[10]
private p_z_d6[10],p_z_d7[10],p_z_d8[10],p_z_d9[10]

private p_b_d1[10],p_b_d2[10],p_b_d3[10],p_b_d4[10],p_b_d5[10]
private p_b_d6[10],p_b_d7[10],p_b_d8[10],p_b_d9[10]

private p_n_d1[10],p_n_d2[10],p_n_d3[10],p_n_d4[10],p_n_d5[10]
private p_n_d6[10],p_n_d7[10],p_n_d8[10],p_n_d9[10]

afill(p_z_d1,0) ; afill(p_z_d2,0) ; afill(p_z_d3,0) ; afill(p_z_d4,0)
afill(p_z_d5,0) ; afill(p_z_d6,0) ; afill(p_z_d7,0) ; afill(p_z_d8,0) ; afill(p_z_d9,0)

afill(p_b_d1,0) ; afill(p_b_d2,0) ; afill(p_b_d3,0) ; afill(p_b_d4,0)
afill(p_b_d5,0) ; afill(p_b_d6,0) ; afill(p_b_d7,0) ; afill(p_b_d8,0) ; afill(p_b_d9,0)

afill(p_n_d1,0) ; afill(p_n_d2,0) ; afill(p_n_d3,0) ; afill(p_n_d4,0)
afill(p_n_d5,0) ; afill(p_n_d6,0) ; afill(p_n_d7,0) ; afill(p_n_d8,0) ; afill(p_n_d9,0)


close all
use razredi new 
go top
for i := 1 to 9
   razred_iz[i] := &("razredi->r"+str(i,1,0))
   razred_PIOiz[i] := &("razredi->p"+str(i,1,0))
next i

close all
use probrac index probrac new

use posl_obr index posl_obr new
zap

use doprinos index doprinos new

use samodop index samodop new

use kredit index kredit new
reindex

use rad_sam index rad_sam new

use porezi index porezi new

use rad_sati index rad_sati new

use obracun index obracun new
zap

use radnici index radnici,radnici2 new




select porezi
seek(1)

vred_boda   := porezi->vr_boda
// BB 10
prvi=.t.

vrbod   := porezi->vr_boda
fcasova     := porezi->fondcasova
casova_za_ob:= porezi->cas_za_ob
akontacija  := porezi->akont
pros_bruto  := porezi->pros_brut
if TO_odbiti
   TO_dnevno   := porezi->TO_minus
endif

prazno:=" "
afill(pren_bruto_zar,0)
afill(pren_bol_bruto,0)
afill(pren_nak_bruto,0)
afill(pren_n_do,0)
afill(pren_n_pr,0)
afill(pren_por,0)
afill(pren_bolbruto,0)
afill(pren_inv,0)

cls

if TO_odbiti
   @ 06,10 say "Prosecna bruto zarada" get pros_bruto_boda picture "999,999.99"
   @ 08,10 say "Placeni topli obrok za 1 dan" get TO_dnevno picture "999,999.99"
   @ 10,10 say "Vrednost boda" get vred_boda picture "999,999.999"
   @ 12,10 say "Fond casova u mesecu" get fcasova picture "999.99"
   @ 14,10 say "Fond casova za obracun" get casova_za_ob picture "999.99"
   @ 16,10 say "Prva akontacija ? (da/ne)" get akontacija valid   ;
       upper(akontacija) == "DA" .or. upper(akontacija) == "NE"
     
   @ 18,10 say "Potvrda" get prazno
   read
else
   @ 06,10 say "Prosecna bruto zarada" get pros_bruto_boda picture "999,999.99"
   @ 08,10 say "Vrednost boda" get vred_boda picture "999,999.999"
   @ 10,10 say "Fond casova u mesecu" get fcasova picture "999.99"
   @ 12,10 say "Fond casova za obracun" get casova_za_ob picture "999.99"
   @ 14,10 say "Prva akontacija ? (da/ne)" get akontacija valid   ;
         upper(akontacija) == "DA" .or. upper(akontacija) == "NE"

   @ 16,10 say "Potvrda" get prazno
   read
endif


porezi->vr_boda   := vred_boda
porezi->fondcasova:= fcasova
porezi->cas_za_ob := casova_za_ob
porezi->akont     := upper(akontacija)
porezi->pros_brut := pros_bruto  
if TO_odbiti
   porezi->TO_minus  := TO_dnevno
endif

select radnici
go top
do while !eof()
 priv1:=radnici->red_broj
 priv2:=radnici->koefic
 if radnici->razred == 0
    radnici->razred := 1
 endif
 priv3:=radnici->razred
 priv4:=radnici->min_rad
 priv5:=radnici->koefic1
 priv6:=radnici->operativni
 priv7:=radnici->oznaka
 if radnici->rad_jed < 1
    radnici->rad_jed := 1
 endif
 priv20:=radnici->rad_jed
 if opstina
   priv71:=radnici->min_plata
 endif
 select obracun
 if upper(radnici->aktivan) == "DA"
   append blank
   obracun->red_broj:= priv1
   obracun->koefic:= priv2
   obracun->razred:= priv3
   if date()<priv4
      centar("Paznja !!!  Minuli rad za radnika " +alltrim(str(priv1,6,0))+ ;
                                                            " nije dobro unet",9)
   else
      datum :=ctod("01."+right("0"+alltrim(str(mes,2,0)),2)+"."+str(god,4,0))
      obracun->min_rad:= min(99,int((datum-priv4) / 365))
      if obracun->min_rad>50
         centar("Paznja !!!  Minuli rad za radnika " +alltrim(str(priv1,6,0))+  ;
                                                            " nije dobro unet",9)
      endif
   endif
   obracun->koefic1    := priv5
   obracun->operativni := priv6
   obracun->oznaka     := priv7
   obracun->rad_jed    := priv20
   if opstina
      obracun->min_plata := priv71
   endif
 endif
 select radnici
 skip
enddo
close radnici


select rad_sati
go top

do while !eof()
 priv1:=rad_sati->red_broj
 priv2:=rad_sati->ucinak
 priv39:=rad_sati->ucinak1
 priv33:=rad_sati->preb_norm
 priv3:=rad_sati->radn_sati
 priv38:=rad_sati->radn_sat1
 priv34:=rad_sati->stimulacij
 priv4:=rad_sati->drzavni
 priv43:=rad_sati->nerdrzavni
 priv5:=rad_sati->nocni
 priv6:=rad_sati->prekovreme
 priv7:=rad_sati->bolovdo60
 priv8:=rad_sati->placeno
 priv9:=rad_sati->nedelja
 priv10:=rad_sati->god_odm
 priv11:=rad_sati->nepla_do30
 priv12:=rad_sati->nepla_pr30
 priv13:=rad_sati->bol_preko6
 priv14:=rad_sati->porodiljsk
 priv15:=rad_sati->invalid
 priv16:=rad_sati->plac_zak
 priv41:=rad_sati->kumul
 priv17:=rad_sati->varijabila
 priv18:=rad_sati->obust_lin1
 priv19:=rad_sati->obust_post
 priv21:=rad_sati->obust_lin2
 priv22:=rad_sati->obust_lin3
 priv23:=rad_sati->obust_lin4
 priv24:=rad_sati->obust_lin5
 priv25:=rad_sati->obust_naz1
 priv26:=rad_sati->obust_naz2
 priv27:=rad_sati->obust_naz3
 priv28:=rad_sati->obust_naz4
 priv29:=rad_sati->obust_naz5
 priv30:=rad_sati->obust_nazp
 priv35:=rad_sati->varp1
 priv36:=rad_sati->varp2
 priv37:=rad_sati->varp3
 priv44:=rad_sati->vezba
 priv45:=rad_sati->bolov100
 priv46:=rad_sati->TO
 if TO_parcijalno
    priv49:=rad_sati->TO_min
 endif
 priv47:=rad_sati->neto_reg
 priv48:=rad_sati->ter_dod
// BB bol
 priv49:=rad_sati->prosek
//


 if br_dana
   priv40:=rad_sati->dana
 endif
 if od_bodova
   priv42:=rad_sati->bodova
 endif


 select obracun
 seek(priv1)
 obracun->ucinak     := priv2
 obracun->ucinak1    := priv39
 obracun->preb_norm  := priv33
 obracun->radn_sati  := priv3
 obracun->radn_sat1  := priv38
 obracun->stimulacij := priv34
 obracun->drzavni    := priv4
 obracun->nerdrzavni := priv43
 obracun->nocni      := priv5
 obracun->prekovreme := priv6
 obracun->bol_do_60  := priv7
 obracun->placeno    := priv8
 obracun->nedelja    := priv9
 obracun->god_odm    := priv10
 obracun->nepla_do30 := priv11
 obracun->nepla_pr30 := priv12
 obracun->bol_preko6 := priv13
 obracun->porodiljsk := priv14
 obracun->invalid    := priv15
 obracun->plac_zak   := priv16
 obracun->kumul      := priv41
 obracun->varijabila := priv17
 obracun->obust_lin1 := priv18
 obracun->obust_post := priv19
 obracun->obust_lin2 := priv21
 obracun->obust_lin3 := priv22
 obracun->obust_lin4 := priv23
 obracun->obust_lin5 := priv24
 obracun->obust_naz1 := priv25
 obracun->obust_naz2 := priv26
 obracun->obust_naz3 := priv27
 obracun->obust_naz4 := priv28
 obracun->obust_naz5 := priv29
 obracun->obust_nazp := priv30
 obracun->varp1      := priv35
 obracun->varp2      := priv36
 obracun->varp3      := priv37
 obracun->vezba      := priv44
 obracun->bolov100   := priv45
 obracun->TO         := priv46
 if TO_parcijalno
    obracun->TO_min:=priv49
 endif
 obracun->neto_reg   := priv47
 obracun->ter_dod    := priv48
// BB bol
 obracun->prosek    := priv49
//
 if br_dana
   obracun->dana     := priv40
 endif
 if od_bodova
   obracun->bodova   := priv42
 endif

 select rad_sati
 skip
enddo
close rad_sati





select porezi
seek(1)
gar_zar     := porezi->zarada
porez_pr    := porezi->akporez
ak_porez2   := porezi->akporez2
ak_porez3   := porezi->akporez3
//ak_porez4 := porezi->akporez4
prva_stopa  := porezi->prvast
druga_stopa := porezi->drugast
pros_bruto  := porezi->pros_brut
//treca_stopa:=porezi->trecast

lin_porez3:=0

//lin_porez3:=porezi->linporez3

pos_por_pr := porezi->posporez
pr_drzavni := porezi->proc_drzavni
pr_nocni   := porezi->proc_nocni
pr_preko   := porezi->proc_preko
pr_minul   := porezi->proc_minul
f_casova   := porezi->fondcasova
// BB 10
if !opstina
vred_boda  := porezi->vr_boda
endif
vrbod  := porezi->vr_boda
//
pr_bolov   := porezi->proc_bolov
pr_plac    := porezi->proc_plac
pr_nedel   := porezi->proc_nedel
pr_pl_za   := porezi->proc_pl_za
pr_inval   := porezi->proc_inval
akontacija := porezi->akont
close porezi


select doprinos

for i:= 1 to 9

seek(i)
if found()
&("zar_dop" + str(i,1,0)):=doprinos->proc_radn
&("p_zar_dop" + str(i,1,0)):=doprinos->proc_posl
&("bol_dop" + str(i,1,0)):=doprinos->b60_proc_r
&("p_bol_dop" + str(i,1,0)):=doprinos->b60_proc_p
&("nep_dop" + str(i,1,0)):=doprinos->nep_proc_p
&("b60_dop" + str(i,1,0)):=doprinos->bp60proc_p
&("b60_fon" + str(i,1,0)):=doprinos->bp60fprocp
&("inv_dop" + str(i,1,0)):=doprinos->inv_proc_p
&("por_dop" + str(i,1,0)):=doprinos->por_proc_p
else
&("bol_dop" + str(i,1,0)):=0
&("p_bol_dop" + str(i,1,0)):=0
&("zar_dop" + str(i,1,0)):=0
&("p_zar_dop" + str(i,1,0)):=0
&("nep_dop" + str(i,1,0)):=0
&("b60_dop" + str(i,1,0)):=0
&("b60_fon" + str(i,1,0)):=0
&("inv_dop" + str(i,1,0)):=0
&("por_dop" + str(i,1,0)):=0
endif

next i


select doprinos

for i:= 1 to 9
   seek(i)
   if found()
      if doprinos->proc_radn > 9 .and. doprinos->proc_radn < 15
         dop_PIO := doprinos->proc_radn
      endif
   endif
next i

close doprinos


zar_dop:=zar_dop1 + zar_dop2 + zar_dop3 + zar_dop4 + zar_dop5 + zar_dop6 + ;
         zar_dop7 + zar_dop8 + zar_dop9
bol_dop:=bol_dop1 + bol_dop2 + bol_dop3 + bol_dop4 + bol_dop5 + bol_dop6 +;
         bol_dop7 + bol_dop8 + bol_dop9


cena_casa := vred_boda / fcasova

select obracun
go top
do while !eof()

// BB:
if opstina
  if (prvi) 
     vred_boda := vrbod
  else
     vred_boda := vrbod*(1-obracun->min_plata/100)
  endif
endif

obracun->ukup_cas:=obracun->ucinak+obracun->ucinak1+obracun->radn_sat1+;
obracun->radn_sati + obracun->nocni+ obracun->nedelja + obracun->drzavni


ukup1_cas:=obracun->ucinak+obracun->ucinak1+obracun->radn_sat1+;
obracun->radn_sati + obracun->drzavni + obracun->nocni+ obracun->bol_do_60 + ;
obracun->placeno +obracun->nepla_do30+obracun->nepla_pr30+obracun->invalid+ ;
obracun->plac_zak + obracun->bol_preko60+obracun->porodiljsk+obracun->nedelja+;
obracun->nerdrzavni +obracun->god_odm+obracun->vezba+obracun->bolov100



if kupac=20     //  Galantex
   select probrac
	seek(obracun->red_broj)
   if found()
      if probrac->bodovi > probrac->maxbod
         bod:=probrac->maxbod
      else
         bod:=probrac->bodovi
      endif
   else
      bod:=0
   endif
   select obracun

   if od_bodova
      bod := obracun->bodova
   endif

   if obracun->ucinak=0
      obracun->uk_r_sati:=bod*if(br_dana,8*obracun->dana,f_casova)/     ;
            obracun->koefic +(obracun->radn_sati + obracun->drzavni +     ;
            obracun->nocni + obracun->prekovreme +  obracun->nedelja)*     ;
            (1 + obracun->stimulacij/100)
      obracun->neto_zar:=obracun->uk_r_sati * obracun->koefic * vred_boda /  ;
            if(br_dana,8*obracun->dana,f_casova) + obracun->radn_sat1*    ;
            obracun->koefic1* vred_boda / if(br_dana,8*obracun->dana,f_casova)
   else
      obracun->uk_r_sati:=bod*if(br_dana,8*obracun->dana,f_casova)/obracun->koefic +     ;
            (obracun->radn_sati + obracun->drzavni + obracun->nocni +   ;
            obracun->prekovreme + obracun->nedelja)*(1 + obracun->stimulacij/100)
      obracun->neto_zar:=obracun->uk_r_sati * obracun->koefic * vred_boda /  ;
            if(br_dana,8*obracun->dana,f_casova) + obracun->radn_sat1* obracun->koefic1*     ;
            vred_boda / if(br_dana,8*obracun->dana,f_casova)
      if obracun->preb_norm<>0
         obracun->neto_zar:=obracun->neto_zar*obracun->preb_norm/100
      endif
   endif
else
   if !od_bodova
      obracun->uk_r_sati:=obracun->ucinak*obracun->preb_norm/100 *        ;
         if(stim_proiz , 1 + obracun->stimulacij/100, 1) + ;
         (obracun->radn_sati + obracun->drzavni + obracun->nocni +  ;
         obracun->prekovreme + obracun->nedelja)*(1 + obracun->stimulacij/100)
      obracun->neto_zar:=obracun->uk_r_sati * obracun->koefic * vred_boda /    ;
         if(br_dana,8*obracun->dana,f_casova) +  obracun->koefic1 * vred_boda *  ;
         (obracun->radn_sat1 * (1 + obracun->stimulacij/100) + ;
          obracun->ucinak1 * obracun->preb_norm/100 * ;
          if(stim_proiz , 1 + obracun->stimulacij/100, 1) )         ;
          / if(br_dana,8*obracun->dana,f_casova)

   else
      if abs(((obracun->ucinak*obracun->preb_norm/100 * obracun->koefic +   ;
               obracun->ucinak1*obracun->preb_norm/100 * obracun->koefic1)/   ;
               if(br_dana,8*obracun->dana,f_casova) - obracun->bodova)/    ;
               obracun->koefic  ) > 0.02
         centar("Lose uneti bodovi za radnika "+str(obracun->red_broj,3,0),6)
         ? chr(7)
      endif

      obracun->uk_r_sati:=(obracun->radn_sati + obracun->drzavni + obracun->nocni +  ;
         obracun->prekovreme + obracun->nedelja)*(1 + obracun->stimulacij/100) 

      obracun->neto_zar:=obracun->uk_r_sati * obracun->koefic * vred_boda /  ;
         if(br_dana,8*obracun->dana,f_casova) + obracun->radn_sat1 *      ;
         obracun->koefic1* vred_boda / if(br_dana,8*obracun->dana,f_casova)+;
         obracun->bodova * vred_boda * (1+obracun->stimulacij/100)
   endif
endif

if zaokruzi
   obracun->neto_zar:=round(obracun->neto_zar,0)
endif

  obracun->zar_po_cas := vred_boda / fcasova

  koefi1 := (((obracun->radn_sati + obracun->drzavni + obracun->nocni +  ;
     obracun->prekovreme + obracun->nedelja) * obracun->koefic +    ;
     obracun->radn_sat1 * obracun->koefic1 )  *   ;
     (1 + obracun->stimulacij/100)       +     ;
    (obracun->ucinak * obracun->koefic + obracun->ucinak1 * obracun->koefic1 )  *   ;
     obracun->preb_norm/100  )   ;
     / (obracun->radn_sati + obracun->drzavni + obracun->nocni +  ;
        obracun->prekovreme + obracun->nedelja + obracun->radn_sat1 + ;
        obracun->ucinak + obracun->ucinak1 )

     
   if koefi1 > 0
      obracun->zar_po_cas := obracun->zar_po_cas * koefi1
   else
      obracun->zar_po_cas := obracun->zar_po_cas * obracun->koefic
   endif
         



 //  obracun->zar_po_cas := obracun->neto_zar / (obracun->prekovreme + obracun->ukup_cas)

if kupac == 5  // ATP
   obracun->min_rad_iz:=(obracun->neto_zar+obracun->neto_plac+    ;
         obracun->neto_pl_z + obracun->neto_nerd+obracun->neto_g_od) *     ;
         pr_minul * obracun->min_rad / 100
else
  obracun->min_rad_iz:=(obracun->neto_zar) * pr_minul * obracun->min_rad / 100;
        / (1+obracun->stimulacij/100)
  obracun->min_po_cas := obracun->min_rad_iz / (obracun->ukup_cas + obracun->prekovreme)
endif

if zaokruzi
obracun->min_rad_iz:=round(obracun->min_rad_iz,0)
endif


if .f.  //kupac=20       Galantex
   obracun->neto_prek:=obracun->prekovreme*pr_preko/100*            ;
         (1+obracun->stimulacij/100)* obracun->koefic * vred_boda /     ;
         if(br_dana,8*obracun->dana,f_casova)
   if obracun->preb_norm<>0
      obracun->neto_prek:=obracun->neto_prek*obracun->preb_norm/100
   endif
else
   obracun->neto_prek:=obracun->prekovreme*pr_preko/100 * (obracun->zar_po_cas + obracun->min_po_cas)
endif

if od_neta
   obracun->neto_zar := obracun->varijabila / 0.697
endif

if .f.  //  kupac=20      Galantex
   obracun->neto_drza:=obracun->drzavni*pr_drzavni/100*        ;
         (1+obracun->stimulacij/100) * obracun->koefic * vred_boda /    ;
         if(br_dana,8*obracun->dana,f_casova)
   if obracun->preb_norm<>0
      obracun->neto_drza:=obracun->neto_drza*obracun->preb_norm/100
   endif
else
   obracun->neto_drza:=obracun->drzavni*pr_drzavni/100 * (obracun->zar_po_cas + obracun->min_po_cas)
endif

if zaokruzi
   obracun->neto_drza:=round(obracun->neto_drza,0)
endif


if .f.  // kupac=20      Galantex
   obracun->neto_nocni:=obracun->nocni*(1+obracun->stimulacij/100)*pr_nocni/ ;
         100 * obracun->koefic * vred_boda / if(br_dana,8*obracun->dana,f_casova)
   if obracun->preb_norm<>0
      obracun->neto_nocni:=obracun->neto_nocni*obracun->preb_norm/100
   endif
else
   obracun->neto_nocni:=obracun->nocni*pr_nocni/100 * (obracun->zar_po_cas + obracun->min_po_cas)
endif


// BB:
//   obracun->neto_TO := 0.20 * pros_bruto / f_casova * 8 * obracun->TO
   obracun->neto_TO := obracun->TO

   if TO_odbiti
      if TO_parcijalno
         obracun->TO_minus := TO_dnevno * obracun->TO_min
      else
         obracun->TO_minus := TO_dnevno * obracun->TO
      endif
   endif
   obracun->neto_ter := 0.03 * pros_bruto * obracun->ter_dod


obracun->dodaci := obracun->min_rad_iz + obracun->neto_prek +  ;
                   obracun->neto_drza + obracun->neto_nocni +  ;
                   obracun->neto_TO + obracun->neto_reg + obracun->neto_ter


//   Naknade   **************************************


if (obracun->radn_sati+obracun->radn_sat1+obracun->ucinak+obracun->ucinak1+ ;
   obracun->drzavni+obracun->nocni+obracun->prekovreme +obracun->nedelja) <> 0

   k:=(obracun->neto_zar )/(obracun->radn_sati+obracun->radn_sat1;
         +obracun->ucinak+obracun->ucinak1+obracun->drzavni+obracun->nocni+  ;
         obracun->prekovreme+obracun->nedelja)
else
   k:=(1+obracun->stimulacij/100 ) * obracun->koefic * vred_boda /   ;
         if(br_dana,8*obracun->dana,f_casova)
endif

if .t.    // kupac<>8
   obracun->dod_na_m1 :=  obracun->koefic * cena_casa * obracun->god_odm * ;
                     obracun->min_rad * pr_minul / 100

//   obracun->neto_g_od:=obracun->zar_po_cas*obracun->god_odm + obracun->dod_na_m1
   obracun->neto_g_od:=obracun->prosek*obracun->god_odm
   if kupac = 20
      obracun->neto_g_od:=k*obracun->god_odm
   endif
else
   obracun->neto_g_od:=obracun->god_odm * obracun->koefic * vred_boda /   ;
         if(br_dana,8*obracun->dana,f_casova)
endif
if zaokruzi
obracun->neto_g_od:=round(obracun->neto_g_od,0)
endif



if .f.     //kupac=20      Galantex
   obracun->neto_nerd:=obracun->nerdrzavni  *(1+obracun->stimulacij/100)*   ;
         obracun->koefic * vred_boda / if(br_dana,8*obracun->dana,f_casova)
   if obracun->preb_norm<>0
      obracun->neto_nerd:=obracun->neto_nerd*obracun->preb_norm/100
   endif
else
   obracun->dod_na_m2 :=  obracun->koefic * cena_casa * obracun->nerdrzavni * ;
                     obracun->min_rad * pr_minul / 100
//   obracun->neto_nerd:=obracun->zar_po_cas*obracun->nerdrzavni + obracun->dod_na_m2
   obracun->neto_nerd:=obracun->prosek*obracun->nerdrzavni
endif




if kupac<>8
   obracun->dod_na_m3 := obracun->koefic * cena_casa * obracun->placeno * ;
                     obracun->min_rad * pr_minul * pr_plac / 100 /100
//   obracun->neto_plac:=obracun->zar_po_cas*obracun->placeno* pr_plac / 100 + ;
//                                             obracun->dod_na_m3
   obracun->neto_plac:=obracun->prosek*obracun->placeno* pr_plac/100
else
   obracun->neto_plac:=obracun->placeno*pr_plac/100 * obracun->koefic *   ;
         vred_boda / if(br_dana,8*obracun->dana,f_casova)
endif
if kupac=20 .and. obracun->preb_norm<>0     //  Galantex
   obracun->neto_plac:=obracun->neto_plac*obracun->preb_norm/100
endif



if (obracun->radn_sati+obracun->radn_sat1 +obracun->ucinak+obracun->ucinak1+ ;
   obracun->drzavni+obracun->nocni+obracun->prekovreme+obracun->nedelja) <> 0

   k:=(obracun->neto_zar+obracun->min_rad_iz)/(obracun->radn_sati+    ;
         obracun->radn_sat1+obracun->ucinak+obracun->ucinak1+obracun->drzavni;
         +obracun->nocni+obracun->prekovreme+obracun->nedelja)
else
   k:=(1+obracun->stimulacij/100)*obracun->koefic * vred_boda /    ;
         if(br_dana,8*obracun->dana,f_casova) * (pr_minul * obracun->min_rad ;
         / 100 + 1)

endif

if kupac=20     //  Galantex
  if obracun->bol_do_60<=40

    obracun->neto_bol:=0.70*k*obracun->bol_do_60
  else
     if obracun->bol_do_60<=56
       obracun->neto_bol:=0.80*k*obracun->bol_do_60
     else
       obracun->neto_bol:=0.85*k*obracun->bol_do_60
     endif
  endif
else
//   obracun->neto_bol:=pr_bolov/100* obracun->bol_do_60*obracun->koefic *   ;
//         vred_boda / if(br_dana,8*obracun->dana,f_casova)*    ;
//         ( pr_minul * obracun->min_rad / 100+1)
//   obracun->neto_bol:=pr_bolov/100* obracun->bol_do_60*obracun->zar_po_cas * ;
//         ( pr_minul * obracun->min_rad / 100+1)
   obracun->neto_bol:=pr_bolov/100* obracun->bol_do_60*obracun->prosek
endif

if stim_na_bol
   obracun->neto_bol := obracun->neto_bol * (1 + obracun->stimulacij/100)
endif

if zaokruzi
obracun->neto_bol:=round(obracun->neto_bol,0)
endif



if kupac<>8
//   obracun->neto_pl_z:=obracun->plac_zak*pr_pl_za/100 *      ;
//         (1+obracun->stimulacij/100)*obracun->koefic * vred_boda /   ;
//         if(br_dana,8*obracun->dana,f_casova)
   obracun->neto_pl_z:=obracun->plac_zak*pr_pl_za/100 *      ;
         (1+obracun->stimulacij/100)*obracun->prosek
else
   obracun->neto_pl_z:=obracun->plac_zak*pr_pl_za/100 *obracun->koefic *     ;
         vred_boda / if(br_dana,8*obracun->dana,f_casova)
endif

if kupac=20  .and. obracun->preb_norm<>0     //  Galantex
   obracun->neto_pl_z:=obracun->neto_pl_z*obracun->preb_norm/100
endif




if kupac=20     //  Galantex
   obracun->neto_nede:=obracun->nedelja*(1+obracun->stimulacij/100)*;
         pr_nedel/100 * obracun->koefic * vred_boda /       ;
         if(br_dana,8*obracun->dana,f_casova)
   if obracun->preb_norm<>0
      obracun->neto_nede:=obracun->neto_nede*obracun->preb_norm/100
   endif
else
   obracun->neto_nede:=obracun->nedelja*pr_nedel/100 * obracun->koefic *    ;
         vred_boda / if(br_dana,8*obracun->dana,f_casova)
endif



//   bolovanje placeno 100%
//obracun->neto_b100 := obracun->bolov100*obracun->koefic *   ;
//         vred_boda / if(br_dana,8*obracun->dana,f_casova) *    ;
//         ( pr_minul * obracun->min_rad / 100+1)
obracun->neto_b100 := obracun->bolov100*obracun->prosek

//   Vojna vezba  sa minulim radom
//obracun->neto_vezba := obracun->vezba*obracun->koefic *   ;
//         vred_boda / if(br_dana,8*obracun->dana,f_casova) //*    ;
//         ( pr_minul * obracun->min_rad / 100+1)
//   Stimulacija na vezbu
//obracun->neto_vezba := obracun->neto_vezba * (1 + obracun->stimulacij/100)
obracun->neto_vezba := obracun->vezba*obracun->prosek








//   =============




//  P - procenat,  L - linearno,   S - stimulacija,  B  -  bodova

if upper(varjab_p1) == "P" .or. varjab_p1 == " "
   obracun->variz1:=(obracun->neto_zar+obracun->neto_g_od+obracun->neto_nerd+ ;
obracun->min_rad_iz+obracun->neto_nocni+obracun->neto_drza)*obracun->varp1/100
endif
if upper(varjab_p2) == "P" .or. varjab_p2 == " "
   obracun->variz2:=(obracun->neto_zar+obracun->neto_g_od+obracun->neto_nerd+ ;
obracun->min_rad_iz+obracun->neto_nocni+obracun->neto_drza)*obracun->varp2/100
endif
if upper(varjab_p3) == "P" .or. varjab_p3 == " "
   obracun->variz3:=(obracun->neto_zar+obracun->neto_g_od+obracun->neto_nerd+ ;
obracun->min_rad_iz+obracun->neto_nocni+obracun->neto_drza)*obracun->varp3/100
endif

if upper(varjab_p1) == "L"
   obracun->variz1:=obracun->varp1
endif
if upper(varjab_p2) == "L"
   obracun->variz2:=obracun->varp2
endif
if upper(varjab_p3) == "L"
   obracun->variz3:=obracun->varp3
endif

if upper(varjab_p1) == "B"
   obracun->variz1:=obracun->varp1*vred_boda
endif
if upper(varjab_p2) == "B"
   obracun->variz2:=obracun->varp2*vred_boda
endif
if upper(varjab_p3) == "B"
   obracun->variz3:=obracun->varp3*vred_boda
endif


if upper(varjab_p1) == "C"
   obracun->variz1:=obracun->varp1*vred_boda*obracun->koefic/fcasova
endif
if upper(varjab_p2) == "C"
   obracun->variz2:=obracun->varp2*vred_boda*obracun->koefic/fcasova
endif
if upper(varjab_p3) == "C"
   obracun->variz3:=obracun->varp3*vred_boda*obracun->koefic/fcasova
endif



if kupac=20     //  Galantex
obracun->variz1:=obracun->variz1+obracun->neto_g_od * obracun->varp1/100
endif


/*
obracun->neto_n_do=obracun->nepla_do30 * obracun->koefic * vred_boda/  ;
      if(br_dana,8*obracun->dana,f_casova)*(pr_minul * obracun->min_rad/100+1)
obracun->neto_n_pr=obracun->nepla_pr30 * obracun->koefic * vred_boda/     ;
      if(br_dana,8*obracun->dana,f_casova)*(pr_minul*obracun->min_rad/100+1)
obracun->neto_por=obracun->porodiljsk*obracun->koefic*(1+obracun->stimulacij/100;
      +obracun->varp1/100)*vred_boda/f_casova*(pr_minul*obracun->min_rad/100+1)
*/
obracun->neto_n_do=obracun->nepla_do30 * obracun->prosek
obracun->neto_n_pr=obracun->nepla_pr30 * obracun->prosek
obracun->neto_por=obracun->porodiljsk*obracun->prosek

if kupac=20     //  Galantex
   obracun->neto_b_pr=0.85*obracun->bol_preko6 * obracun->koefic *    ;
         (1+obracun->stimulacij/100)*(1+obracun->varp1/100)*vred_boda/    ;
         if(br_dana,8*obracun->dana,f_casova)*( pr_minul * obracun->min_rad / 100+1)

   if obracun->preb_norm<>0
      obracun->neto_b_pr:=obracun->neto_b_pr*obracun->preb_norm/100
   endif

else
   if kupac=8  // Zavod za urbanizam
   obracun->neto_b_pr=0.85*obracun->bol_preko6 * obracun->koefic *      ;
         (1+obracun->stimulacij/100)*vred_boda/            ;
         if(br_dana,8*obracun->dana,f_casova)*(pr_minul*obracun->min_rad/100+1)
  else
//      obracun->neto_b_pr=0.85*obracun->bol_preko6*obracun->koefic *     ;
//            (1+obracun->stimulacij/100)*vred_boda/                  ;
//            if(br_dana,8*obracun->dana,f_casova)*(pr_minul*obracun->min_rad/100+1)
      obracun->neto_b_pr=0.65*obracun->bol_preko6*obracun->prosek
   endif
endif

if zaokruzi
   obracun->neto_b_pr:=round(obracun->neto_b_pr,0)
endif


if obracun->invalid<>0
obracun->neto_inv=pr_inval * (obracun->neto_zar;
+obracun->neto_nerd+ obracun->neto_g_od +;
obracun->min_rad_iz+obracun->variz1+obracun->variz2+obracun->variz3)/100
else
obracun->neto_inv:=0
endif

if od_neta
obracun->neto_nak:=obracun->neto_plac+obracun->neto_pl_z+;
               obracun->neto_nerd+obracun->neto_g_od+;
               obracun->neto_nede+obracun->neto_bol + ;
               obracun->neto_vezba + obracun->neto_b100  
else
obracun->neto_nak:=obracun->neto_plac+obracun->neto_pl_z+;
               obracun->neto_nerd+obracun->neto_g_od+;
               obracun->neto_nede+obracun->neto_bol + ;
               obracun->kumul+obracun->varijabila+   ;
               obracun->variz1+obracun->variz2+obracun->variz3+        ;
               obracun->neto_vezba + obracun->neto_b100  
endif



 uk_neto := obracun->neto_zar + obracun->neto_nak + obracun->dodaci 
 
 gnz := gar_zar
 gbz := gnz/(1-zar_dop/100)

   b1 := gbz

   n1 := gnz




obracun->ukup_por:=obracun->porez_iz


obracun->neto := obracun->neto_zar + obracun->neto_nak+ obracun->dodaci


// ****
//     PRED-OBRACUN DOPRINOSA NA TERET RADNIKA

if korektivni_dod

   sata :=  obracun->ucinak + obracun->ucinak1 + obracun->radn_sati + ;
            obracun->drzavni +     ;
            obracun->nocni + obracun->prekovreme +  obracun->nedelja + ;
            obracun->placeno + obracun->plac_zak +    ;
            obracun->nerdrzavni + obracun->god_odm + obracun->bol_do_60 +  ;
            obracun->vezba + obracun->bolov100

   if sata >= fcasova
      granica := razred_iz[obracun->razred]
      granicaPIO := razred_PIOiz[obracun->razred]
   else
      granica := razred_iz[obracun->razred] * sata / fcasova
      granicaPIO := razred_PIOiz[obracun->razred] * sata / fcasova
   endif

   if uk_neto >= granica
      koef := 1
   else
      koef := granica / uk_neto
   endif

   if uk_neto >= granicaPIO
      koefPIO := 1
   else
      koefPIO := granicaPIO / uk_neto
   endif


   rj:=obracun->rad_jed

   if upper(akontacija) == "DA"
      if obracun->neto >= granica
         obracun->bruto_osn := obracun->neto
      endif
      if obracun->neto < granica
         obracun->bruto_osn := GRANICA
      endif
      if obracun->neto <= granica /2
         obracun->bruto_osn := granica / 2
      endif

      if obracun->neto >= granicaPIO
         obracun->brutPIOosn := obracun->neto
      endif
      if obracun->neto < granicaPIO
         obracun->brutPIOosn := granicaPIO
      endif
      if obracun->neto <= granicaPIO /2
         obracun->brutPIOosn := granicaPIO / 2
      endif
   else
      if obracun->neto >= granica
         obracun->bruto_osn := obracun->neto
      else
         obracun->bruto_osn := GRANICA
      endif

      if obracun->neto >= granicaPIO
         obracun->brutPIOosn := obracun->neto
      else
         obracun->brutPIOosn := granicaPIO
      endif
   endif
//*
   //if obracun->bruto_osn > obracun->neto
   if gnz > obracun->neto
      //obracun->kor_dod := (obracun->bruto_osn - obracun->neto) * zar_dop / 86
	  obracun->kor_dod := gnz - obracun->neto
      obracun->dodaci := obracun->dodaci + obracun->kor_dod
      obracun->neto:=obracun->neto_zar + obracun->neto_nak+ obracun->dodaci
   else
      obracun->kor_dod := 0
   endif
*/
endif

//  ***

/*
if opstina
   if obracun->neto < obracun->min_plata
      obracun->kor_dod1 := obracun->min_plata - obracun->neto
      obracun->dodaci := obracun->dodaci + obracun->kor_dod1
      obracun->neto:=obracun->neto_zar + obracun->neto_nak + obracun->dodaci
   endif
endif
*/


if zaokruzi
   obracun->neto:=round(obracun->neto,0)
endif


@  13,40 say obracun->red_broj picture "9999"

// BB PO
   sata :=  obracun->ucinak + obracun->ucinak1 + obracun->radn_sati +  ;
            obracun->drzavni +     ;
            obracun->nocni + obracun->prekovreme +  obracun->nedelja + ;
            obracun->placeno + obracun->plac_zak +    ;
            obracun->nerdrzavni + obracun->god_odm + obracun->bol_do_60 +  ;
            obracun->vezba + obracun->bolov100
// OBRACUN POREZA
// obracun->porez_iz :=  obracun->neto * porez_pr / 100
	if obracun->neto > prva_stopa
	   if sata >= fcasova
	     obracun->porez_iz := (obracun->neto - prva_stopa)  * porez_pr / 100
	     obracun->umanjenje := prva_stopa
	   else
	     obracun->porez_iz := (obracun->neto - prva_stopa*sata/fcasova)  * porez_pr / 100
	     obracun->umanjenje := prva_stopa*sata/fcasova
	   endif
	else
	     obracun->porez_iz := 0
	     obracun->umanjenje := obracun->neto
	endif


//     OBRACUN DOPRINOSA NA TERET RADNIKA


 if sata >= fcasova
   granica := razred_iz[obracun->razred]
   granicaPIO := razred_PIOiz[obracun->razred]
 else
   granica := razred_iz[obracun->razred] * sata / fcasova
   granicaPIO := razred_PIOiz[obracun->razred] * sata / fcasova
 endif

 if uk_neto >= granica
   koef := 1
 else
   koef := granica / uk_neto
 endif

 if uk_neto >= granicaPIO
   koefPIO := 1
 else
   koefPIO := granicaPIO / uk_neto
 endif



                   //    OBRACUN DOPRINOSA NA TERET RADNIKA

rj:=obracun->rad_jed

if upper(akontacija) == "DA"
   if obracun->neto >= granica
      obracun->bruto_osn := obracun->neto
   endif
   if obracun->neto < granica
      obracun->bruto_osn := GRANICA
   endif
   if obracun->neto <= granica /2
      obracun->bruto_osn := granica / 2
   endif

   if obracun->neto >= granicaPIO
      obracun->brutPIOosn := obracun->neto
   endif
   if obracun->neto < granicaPIO
      obracun->brutPIOosn := granicaPIO
   endif
   if obracun->neto <= granicaPIO /2
      obracun->brutPIOosn := granicaPIO / 2
   endif
else
   if obracun->neto >= granica
      obracun->bruto_osn := obracun->neto
   else
      obracun->bruto_osn := GRANICA
   endif

   if obracun->neto >= granicaPIO
      obracun->brutPIOosn := obracun->neto
   else
      obracun->brutPIOosn := granicaPIO
   endif
endif



for i:=1 to 9    //    NA SAV NETO  !
if i<> PIO_rbr
   &("obracun->dop_zar" + str(i,1,0)):=obracun->bruto_osn * &("zar_dop" + str(i,1,0)) / 100
else
   &("obracun->dop_zar" + str(i,1,0)):=obracun->brutPIOosn * &("zar_dop" + str(i,1,0)) / 100
endif
//prom := "p_z_d"+str(i,1,0)
&("p_z_d"+str(i,1,0))[rj] := &("p_z_d"+str(i,1,0))[rj] + &("obracun->dop_zar"+str(i,1,0))
next i

//for i:=1 to 9
//&("obracun->dop_bol" + str(i,1,0)):=koef * (obracun->neto_bol+obracun->neto_b100) ;
//            /(1-zar_dop/100)  * &("bol_dop" + str(i,1,0)) / 100
//&("p_b_d"+str(i,1,0))[rj] := &("p_b_d"+str(i,1,0))[rj] + &("obracun->dop_bol"+str(i,1,0))
//next i
//
//for i:=1 to 9
//&("obracun->dop_nak" + str(i,1,0)):=koef * obracun->neto_nak /(1-zar_dop/100) ;
//                     * &("zar_dop" + str(i,1,0)) / 100
//&("p_n_d"+str(i,1,0))[rj] := &("p_n_d"+str(i,1,0))[rj] + &("obracun->dop_nak"+str(i,1,0))
//next i
                   // p_z_d    prenos dopinosa na zarade
                   // p_b_d    prenos dopinosa na bolovanje
                   // p_n_d    prenos dopinosa na naknade

rj:=obracun->rad_jed


// BB 10
if (prvi=.f.)
   pren_bruto_zar[rj] := pren_bruto_zar[rj]+obracun->bruto_osn 
//pren_bol_bruto[rj] := pren_bol_bruto[rj] + obracun->bruto_bol
//pren_nak_bruto[rj]:= pren_nak_bruto[rj] + obracun->bruto_nak
pren_n_do[rj]:=pren_n_do[rj] + obracun->neto_n_do
pren_n_pr[rj]:=pren_n_pr[rj] + obracun->neto_n_pr
pren_por[rj]:=pren_por[rj] + obracun->bruto_por
pren_bolbruto[rj]:=pren_bolbruto[rj]+obracun->neto_b_pr
pren_inv[rj]:=pren_inv[rj] + obracun->neto_inv
endif
// BB 10

priv_neto:=obracun->neto


ukup_bruto := obracun->bruto_zar + obracun->bruto_bol + obracun->bruto_nak

obracun->PIO := ukup_bruto * dop_PIO/100    //  * 0.103

// BB POSPOR
if ak_porez2>0
	   if (obracun->neto-obracun->porez_iz-(obracun->dop_zar1 + obracun->dop_zar2 + obracun->dop_zar3 + obracun->dop_zar4 + obracun->dop_zar5 + obracun->dop_zar6 + obracun->dop_zar7 + obracun->dop_zar8 + obracun->dop_zar9)>60000)
			obracun->pos_por:=((obracun->neto-obracun->porez_iz-(obracun->dop_zar1 + obracun->dop_zar2 + obracun->dop_zar3 + obracun->dop_zar4 + obracun->dop_zar5 + obracun->dop_zar6 + obracun->dop_zar7 + obracun->dop_zar8 + obracun->dop_zar9))-60000)*ak_porez2/100   
		endif
	   if (obracun->neto-obracun->porez_iz-(obracun->dop_zar1 + obracun->dop_zar2 + obracun->dop_zar3 + obracun->dop_zar4 + obracun->dop_zar5 + obracun->dop_zar6 + obracun->dop_zar7 + obracun->dop_zar8 + obracun->dop_zar9)>100000)
			obracun->pos_por:=40000*ak_porez2/100+((obracun->neto-obracun->porez_iz-(obracun->dop_zar1 + obracun->dop_zar2 + obracun->dop_zar3 + obracun->dop_zar4 + obracun->dop_zar5 + obracun->dop_zar6 + obracun->dop_zar7 + obracun->dop_zar8 + obracun->dop_zar9))-100000)*ak_porez3/100
		endif	
endif

//    *********************  OBRACUN SAMODOPRINOSA  ****************

priv7:=obracun->red_broj
select rad_sam

priv1 := priv2 := priv3 := priv4 := priv5 := priv6 := 0

for i:=1 to samodopa
seek(priv7 *  1000 + i)
if found()
 &("priv" + str(i,1,0)):=rad_sam->samodoprin
else
 &("priv" + str(i,1,0)):=0
endif

next i

select obracun

for i:=1 to samodopa
&("obracun->sif_sam" + str(i,1,0)):=&("priv" + str(i,1,0))
next i



select samodop


for i:= 1 to samodopa
za_sik:=&("priv" + str(i,1,0))
seek(za_sik)
if found()
 if samodop->procenat <> 0
   if sam_od_neta
      &("priv" + str(i,1,0)):=obracun->neto * (1- zar_dop/100 - porez_pr/100) * samodop->procenat /100
   else
      &("priv" + str(i,1,0)):=obracun->neto * samodop->procenat /100
   endif
 else
   &("priv" + str(i,1,0)):=samodop->liznos
 endif
else
 &("priv" + str(i,1,0)):=0
endif

next i

select obracun



for i:=1 to  samodopa
   if zaokruzi
      &("priv" + str(i,1,0)):=round(&("priv" + str(i,1,0)),0)
   endif
   &("obracun->samodop"+str(i,1,0)):=&("priv"+str(i,1,0))
next i

pri1 := pri2 := pri3 := pri4 := pri5 := pri6 := pri7 := pri8 := pri9 := 0
for i:= 1 to 9
   &("pri"+str(i,1,0)) := &("obracun->dop_zar"+str(i,1,0))
next i



//    *********************  OBRACUN KREDITA  ****************

select kredit

kr1 := kr2 := kr3 := kr4 := kr5 := 0
kr_izn1 := kr_izn2 := kr_izn3 := kr_izn4 := kr_izn5 := 0

seek(god*100000 + mes*1000 + priv7)
i := 1

do while !eof() .and. kredit->godina == god .and.   ;
                  kredit->mesec == mes .and. kredit->radnik == priv7
   if i < 6
      &("kr" + str(i,1,0)):=kredit->samodop
      &("kr_izn" + str(i,1,0)):=kredit->iznos
   else
      @ 20,10 say "Previse kredita za radnika " + str(priv7,3,0)
   endif
   i := i + 1
   skip
enddo

select obracun

for i:=1 to 5
&("obracun->kredit" + str(i,1,0)):=&("kr" + str(i,1,0))
&("obracun->kr_iz" + str(i,1,0)):=&("kr_izn" + str(i,1,0))
next i



//  ******


obracun->za_isplatu:=obracun->neto - priv1 - priv2 - priv3 - priv4 -priv5 -priv6  ;
                     -obracun->porez_iz-pri1-pri2-pri3-pri4-pri5-pri6-pri7-pri8-pri9 ;
                     +obracun->neto_por+obracun->neto_b_pr+obracun->neto_inv
obracun->obust_plin:=obracun->obust_post/100*(obracun->neto-obracun->pos_por+ ;
            obracun->neto_por+obracun->neto_b_pr+obracun->neto_inv+     ;
            obracun->umanjinv+obracun->umanjb60)
obracun->za_isplatu:=obracun->za_isplatu-obracun->obust_lin1-obracun->obust_plin;
                    -obracun->obust_lin2-obracun->obust_lin3-obracun->obust_lin4;
                    -obracun->obust_lin5-obracun->pos_por
obracun->za_isplatu:=obracun->za_isplatu-kr_izn1-kr_izn2-kr_izn3-kr_izn4-kr_izn5
if TO_odbiti
   obracun->za_isplatu:=obracun->za_isplatu - obracun->TO_minus
endif

// BB 10
if (prvi)
  obracun->bruto_zar=obracun->neto
  obracun->bruto_nak=obracun->bruto_osn
  prvi=.f.
else
  skip
  prvi=.t.
endif
//

enddo

close obracun








//         Obracun doprinosa na teret poslodavca

select posl_obr

for rj:=1 to radnih_jed
seek(rj)
if !found()
   append blank
   posl_obr->red_broj:=rj
endif


for i:=1 to 9
IF &("p_zar_dop" + str(i,1,0)) <> &("zar_dop" + str(i,1,0)) 
      &("posl_obr->zar" + str(i,1,0)):=pren_bruto_zar[rj] * &("p_zar_dop" +;
                                 str(i,1,0)) / 100
else
      &("posl_obr->zar" + str(i,1,0)) := &("p_z_d"+str(i,1,0))[rj]
endif
// bb 10
&("posl_obr->zar" + str(i,1,0)):=pren_bruto_zar[rj] * &("p_zar_dop" +;
                                 str(i,1,0)) / 100
next i

//for i:=1 to 9
//IF &("p_zar_dop" + str(i,1,0)) <> &("zar_dop" + str(i,1,0)) 
//      &("posl_obr->bol" + str(i,1,0)):=pren_bol_bruto[rj] * &("p_bol_dop" +;
//                                 str(i,1,0)) / 100
//else
//      &("posl_obr->bol" + str(i,1,0)) := &("p_b_d"+str(i,1,0))[rj]
//endif
//next i
//

// sve naknade osim bolovanja do 60 dana
//for i:=1 to 9
//IF &("p_zar_dop" + str(i,1,0)) <> &("zar_dop" + str(i,1,0)) 
//      &("posl_obr->nak" + str(i,1,0)):=pren_nak_bruto[rj] *;
//        &("p_zar_dop" + str(i,1,0)) / 100
//else
//      &("posl_obr->nak" + str(i,1,0)) := &("p_n_d"+str(i,1,0))[rj]
//endif
//next i


//doprinosi na nepl preko 30 dana
//for i:=1 to 9
//&("posl_obr->nep" + str(i,1,0)):=pren_n_pr[rj] *  &("nep_dop" + ;
//                                 str(i,1,0)) / 100
//next i

// doprinosi socijalnog na bol preko 30 na teret fonda
//for i:=1 to 9
//&("posl_obr->b60f" + str(i,1,0)):=pren_bolbruto[rj] *  &("b60_fon" + str(i,1,0)) / 100
//next i

// doprinosi socijalnog na neplaceno do 30 dana
//for i:=1 to 9
//&("posl_obr->por" + str(i,1,0)):=pren_n_do[rj] *  &("por_dop" + str(i,1,0)) / 100
//next i
//
// doprinosi socijalnog na invalidsko
//for i:=1 to 9
//&("posl_obr->inv" + str(i,1,0)):=pren_inv[rj] *  &("inv_dop" + str(i,1,0)) / 100
//next i



// doprinosi na teret poslodavca za bol preko 30
//for i:=1 to 9
//&("posl_obr->b60" + str(i,1,0)):=pren_bolbruto[rj] *  &("b60_dop" + str(i,1,0)) / 100
//next i

next rj

close all




use obracun index obracun new
masa :=0
masa1:=0
//sum bruto,bruto_inv to masa, masa1
//masa:=masa+masa1

// bb 10
sumum1:=0
sumum2:=0
sum bruto_zar,bruto_nak to sumum1, sumum2

masa1 :=0

sum neto_zar,neto_bol,neto_nak,dodaci to n_z,n_b,n_n,dod

masa:=n_z+n_n+dod

close obracun

use posl_obr index posl_obr new
reindex

seek(0)
if !found()
append blank
   posl_obr->red_broj:=0
endif

for i:=1 to 9
&("posl_obr->inv" + str(i,1,0)):=0
&("posl_obr->b60" + str(i,1,0)):=0
&("posl_obr->nep" + str(i,1,0)):=0
&("posl_obr->nak" + str(i,1,0)):=0
&("posl_obr->bol" + str(i,1,0)):=0
&("posl_obr->zar" + str(i,1,0)):=0
&("posl_obr->por" + str(i,1,0)):=0
&("posl_obr->b60f" +str(i,1,0)):=0
next i

sum zar1,zar2,zar3,zar4,zar5,zar6,zar7,zar8,zar9,;
    bol1,bol2,bol3,bol4,bol5,bol6,bol7,bol8,bol9,;
    nak1,nak2,nak3,nak4,nak5,nak6,nak7,nak8,nak9,;
    nep1,nep2,nep3,nep4,nep5,nep6,nep7,nep8,nep9,;
    b60f1,b60f2,b60f3,b60f4,b60f5,b60f6,b60f7,b60f8,b60f9,;
    por1,por2,por3,por4,por5,por6,por7,por8,por9,;
    inv1,inv2,inv3,inv4,inv5,inv6,inv7,inv8,inv9,;
    b601,b602,b603,b604,b605,b606,b607,b608,b609 to ;
    s11,s12,s13,s14,s15,s16,s17,s18,s19,;
    s21,s22,s23,s24,s25,s26,s27,s28,s29,;
    s31,s32,s33,s34,s35,s36,s37,s38,s39,;
    s41,s42,s43,s44,s45,s46,s47,s48,s49,;
    s51,s52,s53,s54,s55,s56,s57,s58,s59,;
    s61,s62,s63,s64,s65,s66,s67,s68,s69,;
    s71,s72,s73,s74,s75,s76,s77,s78,s79,;
    s81,s82,s83,s84,s85,s86,s87,s88,s89

seek(0)

for i:=1 to 9
&("posl_obr->zar" + str(i,1,0)):=&("s1"+str(i,1,0))
&("posl_obr->bol" + str(i,1,0)):=&("s2"+str(i,1,0))
&("posl_obr->nak" + str(i,1,0)):=&("s3"+str(i,1,0))
&("posl_obr->nep" + str(i,1,0)):=&("s4"+str(i,1,0))
&("posl_obr->b60f" +str(i,1,0)):=&("s5"+str(i,1,0))
&("posl_obr->por" + str(i,1,0)):=&("s6"+str(i,1,0))
&("posl_obr->inv" + str(i,1,0)):=&("s7"+str(i,1,0))
&("posl_obr->b60" + str(i,1,0)):=&("s8"+str(i,1,0))
next i



for rj:=1 to radnih_jed
seek(rj)
for i:=1 to 9
masa:=masa+ &("posl_obr->inv" + str(i,1,0))
next i
for i:=1 to 9
masa:=masa+ &("posl_obr->b60" + str(i,1,0))
next i
for i:=1 to 9
masa:=masa+ &("posl_obr->nep" + str(i,1,0))
next i
for i:=1 to 9
masa:=masa+ &("posl_obr->nak" + str(i,1,0))
next i
for i:=1 to 9
masa:=masa+ &("posl_obr->bol" + str(i,1,0))
next i
for i:=1 to 9
masa:=masa+ &("posl_obr->zar" + str(i,1,0))
next i
for i:=1 to 9
masa:=masa+ &("posl_obr->por" + str(i,1,0))
next i

next rj


use ak_obrac index ak_obrac new
akmasa :=0
akmasa1:=0
//sum bruto to akmasa
//sum bruto_inv to akmasa1
//sum bruto_b_pr to akmasa1
//akmasa:=akmasa1+akmasa

sum neto_zar,neto_bol,neto_nak,dodaci to n_z,n_b,n_n,dod

akmasa:=n_z+n_n+dod

close ak_obrac

use ak_p_obr index ak_p_obr new
for rj:=1 to radnih_jed
seek(rj)
for i:=1 to 9
akmasa:=akmasa+ &("ak_p_obr->inv" + str(i,1,0))
next i
for i:=1 to 9
akmasa:=akmasa+ &("ak_p_obr->b60" + str(i,1,0))
next i
for i:=1 to 9
akmasa:=akmasa+ &("ak_p_obr->nep" + str(i,1,0))
next i
for i:=1 to 9
akmasa:=akmasa+ &("ak_p_obr->nak" + str(i,1,0))
next i
for i:=1 to 9
akmasa:=akmasa+ &("ak_p_obr->bol" + str(i,1,0))
next i
for i:=1 to 9
akmasa:=akmasa+ &("ak_p_obr->zar" + str(i,1,0))
next i
for i:=1 to 9
akmasa:=akmasa+ &("ak_p_obr->por" + str(i,1,0))
next i
next rj


@ 16,10 say "Ukupna masa za isplatu"
@ 16,35 say masa
@ 17,10 say "Isplacena masa"
@ 17,35 say akmasa
@ 18,10 say "Ostaje za isplatu"
@ 18,35 say masa - akmasa
//@ 19,10 say "UVECANA"
//@ 19,35 say sumum1+sumum2*.179 picture "99,999,999.99"
//@ 19,35 say sumum1+sumum2*.179 picture "99,999,999.99"

@ 21,10 say "Izlaz" get prazno
read

select posl_obr

close all
sigurno({"probrac","posl_obr","obracun"})
return

















procedure kaskade()
local gar_zar
local zar_dop := 0
local p_granica[10]
local p_kolko[10]
local p_neto[10]
local p_porez[10]
local p_doprin[10]
local p_bruto[10]
local n1,n3,n6,n10
local b1,b3,b6,b10

afill(p_granica ,"             ")
afill(p_kolko ,0)
afill(p_neto ,0)
afill(p_porez,0)
afill(p_doprin,0)
afill(p_bruto,0)

use doprinos index doprinos new

for i:= 1 to 9
   seek(i)
   if found()
      zar_dop := zar_dop +doprinos->proc_radn
   endif
next i
close doprinos

use porezi index porezi new
seek(1)
gar_zar:=porezi->zarada

 gnz := gar_zar
 gbz := gnz/(1-zar_dop/100)

 b1 := gbz
 b2 := 2 * gbz
 b3 := 3 * gbz
 b6 := 6 * gbz
 b10:= 10 * gbz

   n1 := gnz
   n2 := b2*(1-zar_dop/100) -  b1*0.15
   n3 := b3*(1-zar_dop/100) -  b1*0.3
   n6 := b6*(1-zar_dop/100) -  b1*0.9
   n10:= b10*(1-zar_dop/100) - b1*1.9

close porezi

use obracun index obracun new
go top

SET CONSOLE OFF
set device to printer

do while !eof()

 uk_neto := obracun->neto_zar + obracun->neto_nak + obracun->neto_bol
 uk_bruto:=obracun->bruto

     p_granica[1] := "<"+ str(n1,12,2)
     p_granica[2] := "<"+ str(n3,12,2)
     p_granica[3] := "<"+ str(n6,12,2)
     p_granica[4] := "<"+ str(n10,12,2)
     p_granica[5] := ">"+ str(n10,12,2)
   do case
      case uk_neto <= n1

         p_kolko[1] := p_kolko[1] + 1
         p_neto[1] := p_neto[1] + uk_neto
         p_bruto[1] := p_bruto[1] + uk_bruto
         p_doprin[1] := p_doprin[1] + uk_bruto*zar_dop/100
         p_porez[1] := p_porez[1] + uk_bruto*0

      case uk_neto <= n3

         p_kolko[2] := p_kolko[2] + 1
         p_neto[2] := p_neto[2] + uk_neto
         p_bruto[2] := p_bruto[2] + uk_bruto
         p_doprin[2] := p_doprin[2] + uk_bruto*zar_dop/100
         p_porez[2] := p_porez[2] + uk_bruto*(1-zar_dop/100)-uk_neto

      case uk_neto <= n6

         p_kolko[3] := p_kolko[3] + 1
         p_neto[3] := p_neto[3] + uk_neto
         p_bruto[3] := p_bruto[3] + uk_bruto
         p_doprin[3] := p_doprin[3] + uk_bruto*zar_dop/100
         p_porez[3] := p_porez[3] + uk_bruto*(1-zar_dop/100)-uk_neto

      case uk_neto <= n10

         p_kolko[4] := p_kolko[4] + 1
         p_neto[4] := p_neto[4] + uk_neto
         p_bruto[4] := p_bruto[4] + uk_bruto
         p_doprin[4] := p_doprin[4] + uk_bruto*zar_dop/100
         p_porez[4] := p_porez[4] + uk_bruto*(1-zar_dop/100)-uk_neto
    
      case uk_neto > n10

         p_kolko[5] := p_kolko[5] + 1
         p_neto[5] := p_neto[5] + uk_neto
         p_bruto[5] := p_bruto[5] + uk_bruto
         p_doprin[5] := p_doprin[5] + uk_bruto*zar_dop/100
         p_porez[5] := p_porez[5] + uk_bruto*(1-zar_dop/100)-uk_neto
    
   endcase

 skip

enddo

@ prow(),1 say siroka_slova

@ prow()+1 , 1 say "    Granica   Radnika       Bruto       Porez   Doprinosi        Neto"
@ prow()+1 , 1 say "__________________________________________________________________________"
@ prow()+2 , 1 say "   "

for i:=1 to 5
@ prow()+1,1 say p_granica[i] + str(p_kolko[i],8,0)+str(p_bruto[i],12,2)+  ;
         str(p_porez[i],12,2)+ str(p_doprin[i],12,2)+str(p_neto[i],12,2)

    p_kolko[10] := p_kolko[10] + p_kolko[i]
    p_bruto[10] := p_bruto[10] + p_bruto[i]
    p_porez[10] := p_porez[10] + p_porez[i]
    p_doprin[10]:= p_doprin[10]+ p_doprin[i]
    p_neto[10]  := p_neto[10]  + p_neto[i]

next i
@ prow()+1,1 say "____________________________________________________________________________"
@ prow()+2,1 say p_granica[10] + str(p_kolko[10],8,0)+str(p_bruto[10],12,2)+str(p_porez[10],12,2)+         ;
    str(p_doprin[10],12,2)+str(p_neto[10],12,2)

eject

close all

SET CONSOLE ON
set device to screen

return







procedure smanjenje()
local gar_zar
local zar_dop := 0

local p_granica[10]
local p_kolko[10]
local p_neto[10]
local p_porez[10]
local p_doprin[10]
local p_bruto[10]
local p_PIO[10]
local p_popust[10]

local n1,n3,n6,n10
local b1,b3,b6,b10


afill(p_granica ,"         ")
afill(p_kolko ,0)
afill(p_neto ,0)
afill(p_porez,0)
afill(p_doprin,0)
afill(p_bruto,0)
afill(p_PIO,0)
afill(p_popust,0)

use doprinos index doprinos new

for i:= 1 to 9
   seek(i)
   if found()
      zar_dop := zar_dop +doprinos->proc_radn
   endif
next i
close doprinos

use porezi index porezi new
seek(1)
gar_zar:=porezi->zarada

 gnz := gar_zar
 gbz := gnz/(1-zar_dop/100)

close porezi

use obracun index obracun new
go top

SET CONSOLE OFF
set device to printer

do while !eof()

 uk_neto := obracun->neto_zar + obracun->neto_nak + obracun->neto_bol
 uk_bruto:=obracun->bruto

     p_granica[1] := "<= 400.00"
     p_granica[2] := "<= 600.00"
     p_granica[3] := "<= 750.00"
     p_granica[4] := ">= 750.01"

  do case
      case uk_neto <= 400 

         if uk_neto > 0
            p_kolko[1] := p_kolko[1] + 1
            p_neto[1] := p_neto[1] + uk_neto
            p_bruto[1] := p_bruto[1] + uk_bruto
            p_doprin[1] := p_doprin[1] + uk_bruto*zar_dop/100
            p_porez[1] := p_porez[1] + uk_bruto*(1-zar_dop/100)-uk_neto
            p_PIO[1] := p_PIO[1] + obracun->PIO
            p_popust[1] := p_popust[1] + obracun->popust
         endif

      case uk_neto <= 600

         p_kolko[2] := p_kolko[2] + 1
         p_neto[2] := p_neto[2] + uk_neto
         p_bruto[2] := p_bruto[2] + uk_bruto
         p_doprin[2] := p_doprin[2] + uk_bruto*zar_dop/100
         p_porez[2] := p_porez[2] + uk_bruto*(1-zar_dop/100)-uk_neto
         p_PIO[2] := p_PIO[2] + obracun->PIO
         p_popust[2] := p_popust[2] + obracun->popust

      case uk_neto <= 750

         p_kolko[3] := p_kolko[3] + 1
         p_neto[3] := p_neto[3] + uk_neto
         p_bruto[3] := p_bruto[3] + uk_bruto
         p_doprin[3] := p_doprin[3] + uk_bruto*zar_dop/100
         p_porez[3] := p_porez[3] + uk_bruto*(1-zar_dop/100)-uk_neto
         p_PIO[3] := p_PIO[3] + obracun->PIO
         p_popust[3] := p_popust[3] + obracun->popust

      case uk_neto > 750

         p_kolko[4] := p_kolko[4] + 1
         p_neto[4] := p_neto[4] + uk_neto
         p_bruto[4] := p_bruto[4] + uk_bruto
         p_doprin[4] := p_doprin[4] + uk_bruto*zar_dop/100
         p_porez[4] := p_porez[4] + uk_bruto*(1-zar_dop/100)-uk_neto
         p_PIO[4] := p_PIO[4] + obracun->PIO
         p_popust[4] := p_popust[4] + obracun->popust
    
   endcase

 skip

enddo

@ prow(),1 say siroka_slova

@ prow()+1 , 1 say "Granica Radnika      Bruto      Porez  Doprinosi       Neto       PIO  PIO za upl."
@ prow()+1 , 1 say "_________________________________________________________________________________"
@ prow()+2 , 1 say "   "

for i:=1 to 4
@ prow()+1,1 say p_granica[i] + str(p_kolko[i],6,0)+str(p_bruto[i],11,2)+  ;
         str(p_porez[i],11,2)+ str(p_doprin[i],11,2)+str(p_neto[i],11,2)  + ;
         str(p_PIO[i],11,2) + str(p_PIO[i]-p_popust[i],11,2)

    p_kolko[10] := p_kolko[10] + p_kolko[i]
    p_bruto[10] := p_bruto[10] + p_bruto[i]
    p_porez[10] := p_porez[10] + p_porez[i]
    p_doprin[10]:= p_doprin[10]+ p_doprin[i]
    p_neto[10]  := p_neto[10]  + p_neto[i]
    p_PIO[10]   := p_PIO[10]   + p_PIO[i]
    p_popust[10]:= p_popust[10] + p_popust[i]

next i
@ prow()+1,1 say "_________________________________________________________________________________"
@ prow()+2,1 say p_granica[10] + str(p_kolko[10],6,0)+str(p_bruto[10],11,2)+ ;
         str(p_porez[10],11,2)+ str(p_doprin[10],11,2)+str(p_neto[10],11,2) + ;
         str(p_PIO[10],11,2) + str(p_PIO[10]-p_popust[10],11,2)

eject

close all

SET CONSOLE ON
set device to screen

return




























function razredjen(string,razred)
local ret
ret := ""

for i := 1 to len(alltrim(string))
   ret := ret + substr(string,i,1) + space(razred)
next i

return(ret)



procedure OD_obrazac()
local gar_zar
local zar_dop := 0
local akontacija
local kopija := 1

local razred_iz[10]
local razred_PIOiz[10]

local zz01[10]
local zz01_01[10]
local zz01_02[10]
local zz01_02_01[10]
local zz01_02_02[10]
local zz01_02_03[10]
local zz01_02_04[10]
local zz01_03[10]
local zz02[10]
local zz02_01[10]
local zz02_02[10]
local zz02_03[10]
local zz03[10]
local zz03_01[10]
local zz03_02[10]
local zz03_03[10]
local zz04[10]
local zz05[10]
local zz05_01[10]
local zz05_01_01[10]
local zz05_01_02[10]
local zz05_01_03[10]
local zz05_02[10]
local zz05_02_01[10]
local zz05_02_02[10]
local zz05_02_03[10]
local zz06[10]
local zz07[10]
local zz08[10]
local zz08_01[10]
local zz08_02[10]
local zz09[10]
local zz09_01[10]
local zz09_01_01[10]
local zz09_01_02[10]
local zz09_01_03[10]
local zz09_02[10]
local zz09_02_01[10]
local zz09_02_02[10]
local zz09_02_03[10]
local zz10[10]
local zz11[10]
local zz12[10]
local zz12_01[10]
local zz12_01_01[10]
local zz12_01_02[10]
local zz12_01_03[10]
local zz12_02[10]
local zz12_02_01[10]
local zz12_02_02[10]
local zz12_02_03[10]
local zz13[10]
local zz14[10]
local zz14_01[10]
local zz14_01_01[10]
local zz14_01_02[10]
local zz14_01_03[10]
local zz14_02[10]
local zz14_02_01[10]
local zz14_02_02[10]
local zz14_02_03[10]
local zz15[10]
local zz15_01[10]
local zz15_01_01[10]
local zz15_01_02[10]
local zz15_01_03[10]
local zz15_02[10]
local zz15_02_01[10]
local zz15_02_02[10]
local zz15_02_03[10]

local bb01[5]
local bb02[5]
local bb03[5]
local bb04[5]
local bb05[5]
local bb06[5]
local bb07[5]
local bb08[5]
local bb09[5]
local bb10[5]
local bb11[5]
local bb12[5]
local bb13[5]
local bb14[5]
local bb15[5]
local bb16[5]
local bb17[5]
local bb18[5]
local bb19[5]
local bb20[5]
local bb21[5]
local bb22[5]
local bb23[5]
local bb24[5]
local bb25[5]
local bb26[5]
local bb27[5]
local bb28[5]
local bb29[5]
local bb30[5]
local bb31[5]
local bb32[5]
local bb33[5]
local bb34[5]
local bb35[5]
local bb36[5]
local bb37[5]
local bb38[5]
local bb39[5]
local bb40[5]
local bb41[5]
local bb42[5]
local bb43[5]
local bb44[5]
local bb45[5]
local bb46[5]
local bb47[5]
local bb48[5]
local bb49[5]
local bb50[5]
local bb51[5]
local bb52[5]
local bb53[5]
local bb54[5]
local bb55[5]
local bb56[5]
local bb57[5]
local dzr_rad[9]
local dzr_pos[9]
local dop_PIO, dop_zdrav, dop_zap


use doprinos index doprinos new

select doprinos

for i:= 1 to 9
   seek(i)
   if found()
      if doprinos->proc_radn > 0.1 .and. doprinos->proc_radn < 0.9
         dop_zap := "dop_zar"+str(i,1,0)
      endif
      if doprinos->proc_radn > 5 .and. doprinos->proc_radn < 7
         dop_zdrav := "dop_zar"+str(i,1,0)
      endif
      if doprinos->proc_radn > 9 .and. doprinos->proc_radn <= 14
         dop_PIO := "dop_zar"+str(i,1,0)
      endif
      dzr_rad[i] := doprinos->ziro_racun
      dzr_pos[i] := doprinos->ziro_rac_p
     endif
next i
close doprinos


 cls 
@ 10,10 say "Kopija :" get kopija picture "999"
read
 
for loop:= 1 to kopija

zar_dop := 0

afill(zz01 ,0)
afill(zz01_01 ,0)
afill(zz01_02 ,0)
afill(zz01_02_01 ,0)
afill(zz01_02_02 ,0)
afill(zz01_02_03 ,0)
afill(zz01_02_04 ,0)
afill(zz01_03 ,0)
afill(zz02 ,0)
afill(zz02_01 ,0)
afill(zz02_02 ,0)
afill(zz02_03 ,0)
afill(zz03 ,0)
afill(zz03_01 ,0)
afill(zz03_02 ,0)
afill(zz03_03 ,0)
afill(zz04 ,0)
afill(zz05 ,0)
afill(zz05_01 ,0)
afill(zz05_01_01 ,0)
afill(zz05_01_02 ,0)
afill(zz05_01_03 ,0)
afill(zz05_02 ,0)
afill(zz05_02_01 ,0)
afill(zz05_02_02 ,0)
afill(zz05_02_03 ,0)
afill(zz06 ,0)
afill(zz07 ,0)
afill(zz08 ,0)
afill(zz08_01 ,0)
afill(zz08_02 ,0)
afill(zz09 ,0)
afill(zz09_01 ,0)
afill(zz09_01_01 ,0)
afill(zz09_01_02 ,0)
afill(zz09_01_03 ,0)
afill(zz09_02 ,0)
afill(zz09_02_01 ,0)
afill(zz09_02_02 ,0)
afill(zz09_02_03 ,0)
afill(zz10 ,0)
afill(zz11 ,0)
afill(zz12 ,0)
afill(zz12_01 ,0)
afill(zz12_01_01 ,0)
afill(zz12_01_02 ,0)
afill(zz12_01_03 ,0)
afill(zz12_02 ,0)
afill(zz12_02_01 ,0)
afill(zz12_02_02 ,0)
afill(zz12_02_03 ,0)
afill(zz13 ,0)
afill(zz14 ,0)
afill(zz14_01 ,0)
afill(zz14_01_01 ,0)
afill(zz14_01_02 ,0)
afill(zz14_01_03 ,0)
afill(zz14_02 ,0)
afill(zz14_02_01 ,0)
afill(zz14_02_02 ,0)
afill(zz14_02_03 ,0)
afill(zz15 ,0)
afill(zz15_01 ,0)
afill(zz15_01_01 ,0)
afill(zz15_01_02 ,0)
afill(zz15_01_03 ,0)
afill(zz15_02 ,0)
afill(zz15_02_01 ,0)
afill(zz15_02_02 ,0)
afill(zz15_02_03 ,0)

afill(razred_iz ,0)
afill(razred_PIOiz ,0)

afill(bb01 ,0)
afill(bb02 ,0)
afill(bb03 ,0)
afill(bb04 ,0)
afill(bb05 ,0)
afill(bb06 ,0)
afill(bb07 ,0)
afill(bb08 ,0)
afill(bb09 ,0)
afill(bb10 ,0)
afill(bb11 ,0)
afill(bb12 ,0)
afill(bb13 ,0)
afill(bb14 ,0)
afill(bb15 ,0)
afill(bb16 ,0)
afill(bb17 ,0)
afill(bb18 ,0)
afill(bb19 ,0)
afill(bb20 ,0)
afill(bb21 ,0)
afill(bb22 ,0)
afill(bb23 ,0)
afill(bb24 ,0)
afill(bb25 ,0)
afill(bb26 ,0)
afill(bb27 ,0)
afill(bb28 ,0)
afill(bb29 ,0)
afill(bb30 ,0)
afill(bb31 ,0)
afill(bb32 ,0)
afill(bb33 ,0)
afill(bb34 ,0)
afill(bb35 ,0)
afill(bb36 ,0)
afill(bb37 ,0)
afill(bb38 ,0)
afill(bb39 ,0)
afill(bb40 ,0)
afill(bb41 ,0)
afill(bb42 ,0)
afill(bb43 ,0)
afill(bb44 ,0)
afill(bb45 ,0)
afill(bb46 ,0)
afill(bb47 ,0)
afill(bb48 ,0)
afill(bb49 ,0)
afill(bb50 ,0)
afill(bb51 ,0)
afill(bb52 ,0)
afill(bb53 ,0)
afill(bb54 ,0)
afill(bb55 ,0)
afill(bb56 ,0)
afill(bb57 ,0)

use razredi new 
go top
for i := 1 to 9
   razred_iz[i] := &("razredi->r"+str(i,1,0))
   razred_PIOiz[i] := &("razredi->p"+str(i,1,0))
next i
close all

use doprinos index doprinos new
for i:= 1 to 9
   seek(i)
   if found()
      zar_dop := zar_dop +doprinos->proc_radn
   endif
next i
close all

use porezi index porezi new
seek(1)
gar_zar := porezi->zarada
fcasova := porezi->fondcasova
pros_br := porezi->pros_brut
akontacija := porezi->akont

close porezi


//   OBRACUN PODATAKA ZA  OD   NA OBRACUN  ***************************


use obracun index obracun new
go top

SET CONSOLE OFF
set device to printer

do while !eof()

   uk_neto := obracun->neto   //_zar + obracun->neto_nak + obracun->neto_bol
   uk_bruto:=obracun->bruto

if uk_neto > 1 



      sata :=  obracun->ucinak + obracun->ucinak1 + obracun->radn_sati + ;
               obracun->drzavni +     ;
               obracun->nocni + obracun->prekovreme +  obracun->nedelja + ;
               obracun->placeno + obracun->plac_zak +    ;
               obracun->nerdrzavni + obracun->god_odm + obracun->bol_do_60

      if sata >= fcasova
         granica := razred_iz[obracun->razred]
         granicaPIO := razred_PIOiz[obracun->razred]
      else
         granica := razred_iz[obracun->razred] * sata / fcasova
         granicaPIO := razred_PIOiz[obracun->razred] * sata / fcasova
         bb03[1] := bb03[1] + 1
      endif
   
   if uk_neto <= granica 
      zz01_02_04[obracun->razred] := zz01_02_04[obracun->razred]
      bb01[3] := bb01[3] + obracun->neto
   else                
      bb01[2] := bb01[2] + obracun->neto
   endif

   zz01_01[obracun->razred] := zz01_01[obracun->razred] + obracun->neto_zar
   zz01_01[10] := zz01_01[10] + obracun->neto_zar

   zz01_02_01[obracun->razred] := zz01_02_01[obracun->razred] + obracun->neto_TO  // topli obrok
   zz01_02_01[10] := zz01_02_01[10] + obracun->neto_TO  // topli obrok

   zz01_02_02[obracun->razred] := zz01_02_02[obracun->razred] + obracun->neto_reg  // regres
   zz01_02_02[10] := zz01_02_02[10] + obracun->neto_reg  // regres

   zz01_02_03[obracun->razred] := zz01_02_03[obracun->razred] + obracun->neto_ter  // terenski dodatak
   zz01_02_03[10] := zz01_02_03[10] + obracun->neto_ter  // terenski dodatak

   zz01_02_04[obracun->razred] := zz01_02_04[obracun->razred] + obracun->min_rad_iz +;
            obracun->neto_prek + obracun->neto_nocni + obracun->neto_drza  + obracun->kor_dod + if(opstina,obracun->kor_dod1,0)
   zz01_02_04[10] := zz01_02_04[10] + obracun->min_rad_iz +;
            obracun->neto_prek + obracun->neto_nocni + obracun->neto_drza + obracun->kor_dod + if(opstina,obracun->kor_dod1,0)

   zz01_03[obracun->razred] := zz01_03[obracun->razred] + obracun->neto_bol +;
            obracun->neto_plac + obracun->neto_g_od + obracun->neto_nerd + ;
            obracun->neto_pl_z + obracun->neto_b100 + obracun->neto_nede + ;
            obracun->neto_vezba + obracun->varijabila+   ;
               obracun->variz1+obracun->variz2+obracun->variz3

   zz01_03[10] := zz01_03[10] + obracun->neto_bol +;
            obracun->neto_plac + obracun->neto_g_od + obracun->neto_nerd + ;
            obracun->neto_pl_z + obracun->neto_b100 + obracun->neto_nede + ;
            obracun->neto_vezba + obracun->varijabila+   ;
               obracun->variz1+obracun->variz2+obracun->variz3

   zz02[obracun->razred] := zz02[obracun->razred] + 1
   zz02[10] := zz02[10] + 1

   if uk_neto <= granica 
      zz02_01[obracun->razred] := zz02_01[obracun->razred] + 1
      zz02_01[10] := zz02_01[10] + 1
      bb02[3] := bb02[3] + 1
      if sata < fcasova   
        bb03[3] := bb03[3] + 1
      endif
   else                
      zz02_02[obracun->razred] := zz02_02[obracun->razred] + 1
      zz02_02[10] := zz02_02[10] + 1
      bb02[2] := bb02[2] + 1
      if sata < fcasova   
        bb03[2] := bb03[2] + 1
      endif
   endif
   if uk_neto >= 5 * pros_br    //  Prosecna bruto zarada * 5
      zz02_03[obracun->razred] := zz02_03[obracun->razred] + 1
      zz02_03[10] := zz02_03[10] + 1
   endif
   
   zz03[obracun->razred] := zz02[obracun->razred] + obracun->bruto_osn
   zz03[10] := zz02[10] + obracun->bruto_osn

   if uk_neto <= granica 
      zz03_01[obracun->razred] := zz03_01[obracun->razred] + obracun->bruto_osn
      zz03_01[10] := zz03_01[10] + obracun->bruto_osn
     bb06[3] := bb06[3] + obracun->bruto_osn
     bb09[3] := bb09[3] + &("obracun->"+dop_PIO)
     bb10[3] := bb10[3] + &("obracun->"+dop_zdrav)
     bb11[3] := bb11[3] + &("obracun->"+dop_zap)
   else                
      zz03_02[obracun->razred] := zz03_02[obracun->razred] + obracun->bruto_osn
      zz03_02[10] := zz03_02[10] + obracun->bruto_osn
     bb06[2] := bb06[2] + obracun->bruto_osn
     bb09[2] := bb09[2] + &("obracun->"+dop_PIO)
     bb10[2] := bb10[2] + &("obracun->"+dop_zdrav)
     bb11[2] := bb11[2] + &("obracun->"+dop_zap)
   endif

   if uk_neto >= 5 * pros_br    //  Prosecna bruto zarada * 5
      zz03_03[obracun->razred] := zz03_03[obracun->razred] + obracun->bruto_osn
      zz03_03[10] := zz03_03[10] + obracun->bruto_osn
   endif

   zz05_01_01[obracun->razred] := zz05_01_01[obracun->razred] + &("obracun->"+dop_PIO)
   zz05_01_02[obracun->razred] := zz05_01_02[obracun->razred] + &("obracun->"+dop_zdrav)
   zz05_01_03[obracun->razred] := zz05_01_03[obracun->razred] + &("obracun->"+dop_zap)

   zz05_01_01[10] := zz05_01_01[10] + &("obracun->"+dop_PIO)
   zz05_01_02[10] := zz05_01_02[10] + &("obracun->"+dop_zdrav)
   zz05_01_03[10] := zz05_01_03[10] + &("obracun->"+dop_zap)
   
endif

   skip

enddo

close all

      bb01[1] := bb01[2] + bb01[3]
      bb13[3] := bb09[3] *.11/.13
      bb14[3] := bb10[3] 
      bb15[3] := bb11[3] 
      bb08[3] := round(bb09[3],0) + round(bb10[3],0) + round(bb11[3],0)
      bb12[3] := round(bb13[3],0) + round(bb14[3],0) + round(bb15[3],0)
      bb07[3] := bb08[3] + bb12[3]
      bb13[2] := bb09[2] *.11/.13
      bb14[2] := bb10[2] 
      bb15[2] := bb11[2] 
      bb08[2] := round(bb09[2],0) + round(bb10[2],0) + round(bb11[2],0)
      bb12[2] := round(bb13[2],0) + round(bb14[2],0) + round(bb15[2],0)
      bb07[2] := bb08[2] + bb12[2]
      bb06[1] := bb06[2] + bb06[3]
      bb07[1] := bb07[2] + bb07[3]
      bb08[1] := bb08[2] + bb08[3]
      bb09[1] := bb09[2] + bb09[3]
      bb10[1] := bb10[2] + bb10[3]
      bb11[1] := bb11[2] + bb11[3]
      bb12[1] := bb12[2] + bb12[3]
      bb13[1] := bb13[2] + bb13[3]
      bb14[1] := bb14[2] + bb14[3]
      bb15[1] := bb15[2] + bb15[3]

//   OBRACUN PODATAKA ZA  OD   NA AKONTACIJU  ***************************


use ak_obrac index ak_obrac new
use obracun index obracun new
set relation to red_broj into ak_obrac
select ak_obrac
go top

SET CONSOLE OFF
set device to printer

do while !eof()

   a_uk_neto := ak_obrac->neto   //_zar + ak_obrac->neto_nak + ak_obrac->neto_bol
   a_uk_bruto:=ak_obrac->bruto
   uk_neto := obracun->neto   //_zar + obracun->neto_nak + obracun->neto_bol
   uk_bruto:=obracun->bruto

   if uk_neto > 1 
        sata :=  obracun->ucinak + obracun->ucinak1 + obracun->radn_sati + ;
               obracun->drzavni +     ;
               obracun->nocni + obracun->prekovreme +  obracun->nedelja + ;
               obracun->placeno + obracun->plac_zak +    ;
               obracun->nerdrzavni + obracun->god_odm + obracun->bol_do_60
   end if

if a_uk_neto > 1 



      a_sata :=  ak_obrac->ucinak + ak_obrac->ucinak1 + ak_obrac->radn_sati + ;
               ak_obrac->drzavni +     ;
               ak_obrac->nocni + ak_obrac->prekovreme +  ak_obrac->nedelja + ;
               ak_obrac->placeno + ak_obrac->plac_zak +    ;
               ak_obrac->nerdrzavni + ak_obrac->god_odm + ak_obrac->bol_do_60

      if a_sata >= fcasova
         a_granica := razred_iz[ak_obrac->razred]
         a_granicaPIO := razred_PIOiz[ak_obrac->razred]
      else
         a_granica := razred_iz[ak_obrac->razred] * a_sata / fcasova
         a_granicaPIO := razred_PIOiz[ak_obrac->razred] * a_sata / fcasova
      endif
   
   if uk_neto <= a_granica 
      bb18[3] := bb18[3] + ak_obrac->neto
      bb21[3] := bb21[3] + &("ak_obrac->"+dop_PIO)
      bb22[3] := bb22[3] + &("ak_obrac->"+dop_zdrav)
      bb23[3] := bb23[3] + &("ak_obrac->"+dop_zap)
   else                
      bb18[2] := bb18[2] + ak_obrac->neto
      bb21[2] := bb21[2] + &("ak_obrac->"+dop_PIO)
      bb22[2] := bb22[2] + &("ak_obrac->"+dop_zdrav)
      bb23[2] := bb23[2] + &("ak_obrac->"+dop_zap)
   endif

   zz08[ak_obrac->razred] := zz08[ak_obrac->razred] + ak_obrac->neto
   zz08[10] := zz08[10] + ak_obrac->neto_zar

   zz08_02[ak_obrac->razred] := zz08_02[ak_obrac->razred] + ak_obrac->neto
   zz08_02[10] := zz08_02[10] + ak_obrac->neto_zar


   zz09_01_01[ak_obrac->razred] := zz09_01_01[ak_obrac->razred] + &("ak_obrac->"+dop_PIO)
   zz09_01_02[ak_obrac->razred] := zz09_01_02[ak_obrac->razred] + &("ak_obrac->"+dop_zdrav)
   zz09_01_03[ak_obrac->razred] := zz09_01_03[ak_obrac->razred] + &("ak_obrac->"+dop_zap)

   zz09_01_01[10] := zz09_01_01[10] + &("ak_obrac->"+dop_PIO)
   zz09_01_02[10] := zz09_01_02[10] + &("ak_obrac->"+dop_zdrav)
   zz09_01_03[10] := zz09_01_03[10] + &("ak_obrac->"+dop_zap)


endif

   skip

enddo
      bb25[3] := bb21[3] 
      bb26[3] := bb22[3] 
      bb27[3] := bb23[3] 
      bb20[3] := bb21[3] + bb22[3] + bb23[3]
      bb24[3] := bb25[3] + bb26[3] + bb27[3]
      bb19[3] := bb20[3] + bb24[3]
      bb25[2] := bb21[2] 
      bb26[2] := bb22[2] 
      bb27[2] := bb23[2] 
      bb20[2] := bb21[2] + bb22[2] + bb23[2]
      bb24[2] := bb25[2] + bb26[2] + bb27[2]
      bb19[2] := bb20[2] + bb24[2]
      bb18[1] := bb18[2] + bb18[3]
      bb19[1] := bb19[2] + bb19[3]
      bb20[1] := bb20[2] + bb20[3]
      bb21[1] := bb21[2] + bb21[3]
      bb22[1] := bb22[2] + bb22[3]
      bb23[1] := bb23[2] + bb23[3]
      bb24[1] := bb24[2] + bb24[3]
      bb25[1] := bb25[2] + bb25[3]
      bb26[1] := bb26[2] + bb26[3]
      bb27[1] := bb27[2] + bb27[3]
      bb29[1] := bb01[1] - bb18[1]
for i := 1 to 4 
    bb30[i] := bb07[i] - bb19[i]
    bb31[i] := bb08[i] - bb20[i]
    bb32[i] := bb09[i] - bb21[i]
    bb33[i] := bb10[i] - bb22[i]
    bb34[i] := bb11[i] - bb23[i]
    bb35[i] := bb12[i] - bb24[i]
    bb36[i] := bb13[i] - bb25[i]
    bb37[i] := bb14[i] - bb26[i]
    bb38[i] := bb15[i] - bb27[i]
    
    bb49[i] := bb30[i] - bb40[i]
    bb50[i] := bb31[i] - bb41[i]
    bb51[i] := bb32[i] - bb42[i]
    bb52[i] := bb33[i] - bb43[i]
    bb53[i] := bb34[i] - bb44[i]
    bb54[i] := bb35[i] - bb45[i]
    bb55[i] := bb36[i] - bb46[i]
    bb56[i] := bb37[i] - bb47[i]
    bb57[i] := bb38[i] - bb48[i]
next i

// ************


mes1:= month(date())
god1 := year(date())

do case
case mes1=1
  imemes1:=" januaru"
case mes1=2
  imemes1:=" februaru"
case mes1=3
  imemes1:=" martu"
case mes1=4
  imemes1:=" aprilu"
case mes1=5
  imemes1:=" maju"
case mes1=6
  imemes1:=" junu"
case mes1=7
  imemes1:=" julu"
case mes1=8
  imemes1:=" avgustu"
case mes1=9
  imemes1:=" septembru"
case mes1=10
  imemes1:=" oktobru"
case mes1=11
  imemes1:=" novembru"
case mes1=12
  imemes1:=" decembru"
endcase



do case
case mes=1
  imemes2:=" januar"
case mes=2
  imemes2:=" februar"
case mes=3
  imemes2:=" mart"
case mes=4
  imemes2:=" april"
case mes=5
  imemes2:=" maj"
case mes=6
  imemes2:=" jun"
case mes=7
  imemes2:=" jul"
case mes=8
  imemes2:=" avgust"
case mes=9
  imemes2:=" septembar"
case mes=10
  imemes2:=" oktobar"
case mes=11
  imemes2:=" novembar"
case mes=12
  imemes2:=" decembar"
endcase



@ prow(),1 say uska_slova
@ prow(),1 say "PIB _________________________________"
@ prow(),6 say  rtrim(PIB)
@ prow(),115 say dupla_slova + "PP OD"

@ prow()+1,1 say "Isplatilac _________________________________"
@ prow(),14 say  rtrim(imekor)
@ prow()+1,1 say "Sediste isplatioca ___________________________________"
@ prow(),21 say rtrim(ul_i_br) + ", " + rtrim(br_mesto)

@ prow(),90 say "     REPUBLIKA SRBIJA"
@ prow()+1,1 say "│   │   │   │   │   │   │   │   │  "
@ prow(),3 say razredjen(mat_broj,3)
@ prow(),90 say "  MINISTARSTVO FINANSIJA"
@ prow()+1,1 say "└───┴───┴───┴───┴───┴───┴───┴───┘  "
@ prow(),90 say "      I EKONOMIJE"
@ prow()+1,1 say "         Maticni broj       "

@ prow(),90 say "    -PORESKA UPRAVA-"
@ prow()+1,1 say "Ziro racun isplatioca _____________________"
@ prow(),25 say rtrim(z_r)
@ prow(),90 say "- Organizaciona jedinica"
@ prow()+1,1 say "Sifra delatnosti ispl. _________"
@ prow(),26 say  rtrim(sif_delat)

//@ prow()+1,1 say "Uplatni racun jav. prih. _____________________"
@ prow(),26 say  ""
@ prow(),90 say "------------------------"
//@ prow(),30 say  "840-721111843-18    840-721211843-39"
//@ prow()+1,30 say  "840-721121843-88    840-721221843-12"
//@ prow()+1,30 say  "840-721131843-61    840-721231843-82"

@ prow(),1 say siroka_slova
@ prow()+4,15 say "PORESKA PRIJAVA O OBRACUNATIM I PLACENIM DOPRINOSIMA"
@ prow()+1,15 say "   ZA OBAVEZNO SOCIJALNO OSIGURANJE NA ZARADE "
@ prow()+1,15 say "ZA MESEC " + imemes2 + "  (DEO*___) " + str(god,4,0) + ". GODINE"
@ prow()+1,17 say "Isplata izvrsena u mesecu " + imemes1 + str(god1,5,0) + " godine"
@ prow()+2,40 say "Datum isplate: ____________ godine"

@ prow()+1,1 say uska_slova
@ prow()+1 , 1 say " Isplatilac je (zaokruziti jedan):"
@ prow() , 70 say " Obrac. i placanje doprinosa vrsi se na isplatu:"
@ prow()+1 , 1 say "1. Pravno lice koje se ne finans. iz budzeta "
@ prow() , 70 say "1. Zarada zaposlenih "
@ prow()+1 , 1 say "2. Pravno lice koje se finans. iz budzeta "
@ prow() , 70 say "2. Zaradaosnivaca i clanova preduz. zaposlenih u svom preduz."
@ prow()+1 , 1 say "3. Strano predstavnistvo "
@ prow() , 70 say "3. Razlike zarada izabranih, imenovanih i postavljenih lica"
@ prow()+1 , 1 say "4. Preduzetnik "
@ prow() , 70 say "4. Naknada za privremene i povremene poslove"
@ prow()+1 , 1 say "5. Fizicko lice "
@ prow() , 70 say "5. Naknada za privremene i povremene poslove clanu zadruge"

@ prow()+2 , 1 say "______________________________________________________________________________________________________________________________________"
@ prow()+1 , 1 say "                                             OZNAKA                         O  B  R  A  C  U  N   D  O  P  R  I  N  O  S  A    UPL RAC"
@ prow()+1 , 1 say "Rbr         O P I S                          ZA AOP      UKUPNO       NA ISPLACENU      NA NAJNIZU        NA NAJVIS          JAVN PRIH"
@ prow()+1 , 1 say "                                                                         ZARADU          OSNOVICU         OSNOVICU                    "
@ prow()+1 , 1 say " 1              2                              3           4                5              6                 7                   8 "
@ prow()+1 , 1 say "______________________________________________________________________________________________________________________________________"

@ prow()+2 , 1 say "I     OBRACUNATE ZARADE, OSNOVICE ZA OBRACUN"
@ prow()+1 , 1 say "      DOPRINOSA I OBRACUN DOPRINOSA"

@ prow()+2 , 1 say "1.    Obracunate zarade/naknade"
@ prow()   , 50 say "01"
@ prow() , 41 + 16 say bb01[2] + bb01[3] picture "9999,999"

@ prow()+2 , 1 say "2.    Broj zaposlenih/primalaca naknada"
@ prow()   , 50 say "02"
@ prow() , 41 + 16 say bb02[2] + bb02[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb02[i] picture "9999,999"
next i

@ prow()+2 , 1 say "2.1.  Zaposleni sa nepunim rad vrem (sadr u 2)"
@ prow()   , 50 say "03"
@ prow() , 41 + 16 say bb03[2] + bb03[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb03[i] picture "9999,999"
next i

@ prow()+2 , 1 say "2.2.  Zaposl za koje se ostv. oslob. doprtin "
@ prow()   , 50 say "04"
@ prow() , 41 + 16 say bb04[2] + bb04[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb04[i] picture "9999,999"
next i

@ prow()+2 , 1 say "2.2.1 Zaposl po clanu 45. st.1. (sadr u 2)   "
@ prow()   , 50 say "05"
@ prow() , 41 + 16 say bb04[2] + bb04[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb04[i] picture "9999,999"
next i

@ prow()+2 , 1 say "2.2.2 Zaposl po clanu 45. st.2. (sadr u 2)   "
@ prow()   , 50 say "06"
@ prow() , 41 + 16 say bb05[2] + bb05[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb05[i] picture "9999,999"
next i

@ prow()+2 , 1 say "2.2.3 Zaposl po clanu 45a st.1. (sadr u 2)   "
@ prow()   , 50 say "07"
@ prow() , 41 + 16 say bb05[2] + bb05[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb05[i] picture "9999,999"
next i

@ prow()+2 , 1 say "2.2.4 Zaposl po clanu 45a st.2. (sadr u 2)   "
@ prow()   , 50 say "08"
@ prow() , 41 + 16 say bb05[2] + bb05[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb05[i] picture "9999,999"
next i

@ prow()+2 , 1 say "2.2.5 Zaposl po clanu 45b st.1. (sadr u 2)   "
@ prow()   , 50 say "09"
@ prow() , 41 + 16 say bb05[2] + bb05[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb05[i] picture "9999,999"
next i

@ prow()+2 , 1 say "3.    Osnovica za obrac. doprinosa"
@ prow()   , 50 say "10"
@ prow() , 41 + 16 say bb06[2] + bb06[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb06[i] picture "9999,999"
next i

@ prow()+2 , 1 say "4.    Ukupno obracunati doprinosi "
@ prow()+1 , 1 say "      red. br. 4.1 + red. br. 4.2 "
@ prow()   , 50 say "11"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb07[i] picture "9999,999"
next i

@ prow()+2 , 1 say "4.1   Na teret zaposlenih/primalac"
@ prow()+1 , 1 say "      (red. br. 4.1.1. do 4.1.3)  "
@ prow()   , 50 say "12"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb08[i]  picture "9999,999"
next i

@ prow()+2 , 1 say "4.1.1. Za PIO"
@ prow()   , 50 say "13"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb09[i] picture "9999,999"
next i
@ prow() , 120 say dzr_rad[1] 

@ prow()+2 , 1 say "4.1.2. Za zdravstvo"
@ prow()   , 50 say "14"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb10[i] picture "9999,999"
next i
@ prow() , 120 say dzr_rad[2] 

@ prow()+2 , 1 say "4.1.3. Za zaposljavanje"
@ prow()   , 50 say "15"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb11[i] picture "9999,999"
next i
@ prow() , 120 say dzr_rad[3] 

eject
@ prow()+1 , 110 say "Strana 2"


@ prow()+2 , 1 say "4.2.  Na teret posl.(rb. 4.2.1 do 4.2.3)"
@ prow()   , 50 say "16"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb12[i] picture "9999,999"
next i

@ prow()+2 , 1 say "4.2.1. Za PIO"
@ prow()   , 50 say "17"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb13[i] picture "9999,999"
next i
@ prow() , 120 say dzr_pos[1] 

@ prow()+2 , 1 say "4.2.2. Za zdravstvo"
@ prow()   , 50 say "18"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb14[i] picture "9999,999"
next i
@ prow() , 120 say dzr_pos[2] 

@ prow()+2 , 1 say "4.2.3. Za zaposljavanje"
@ prow()   , 50 say "19"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb15[i] picture "9999,999"
next i
@ prow() , 120 say dzr_pos[3] 

@ prow()+2 , 1 say "5.    Broj zap. za koje se placa dopr. za staz"
@ prow()+1 , 1 say "      osig. sa uvecanim traj. (benef. staz)"
@ prow()   , 50 say "20"


@ prow()+2 , 1 say "6.    Doprinos za PIO za benef. staz"
@ prow()   , 50 say "21"


@ prow()+2 , 1 say "II    RANIJE ISPLACENI DEO ZARADE"
@ prow()+1 , 1 say "      I PLACENI DOPRINOSI"


@ prow()+2 , 1 say "7.    Ranije isplaceni deo zarade/naknade     "
@ prow()   , 50 say "22"
@ prow() , 41 + 16 say bb18[2] + bb18[3] picture "9999,999"
for i := 2 to 4 
   @ prow() , 41 + i*16 say bb18[i] picture "9999,999"
next i


@ prow()+2 , 1 say "8.    Ukupno placeni dop.(rb. 8.1. + 8.2.) "
@ prow()   , 50 say "23"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb19[i] picture "9999,999"
next i

@ prow()+2 , 1 say "8.1.  Od zaposlenih(rb. 8.1.1 do 8.1.3)"
@ prow()   , 50 say "24"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb20[i] picture "9999,999"
next i

@ prow()+2 , 1 say "8.1.1. Za PIO"
@ prow()   , 50 say "25"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb21[i] picture "9999,999"
next i

@ prow()+2 , 1 say "8.1.2. Za zdravstvo"
@ prow()   , 50 say "26"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb22[i] picture "9999,999"
next i

@ prow()+2 , 1 say "8.1.3. Za zaposljavanje"
@ prow()   , 50 say "27"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb23[i] picture "9999,999"
next i

@ prow()+2 , 1 say "8.2.  Na teret posl.(rb. 8.2.1 do 8.2.3)"
@ prow()   , 50 say "28"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb24[i] picture "9999,999"
next i

@ prow()+2 , 1 say "8.2.1. Za PIO"
@ prow()   , 50 say "29"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb25[i] picture "9999,999"
next i

@ prow()+2 , 1 say "8.2.2. Za zdravstvo"
@ prow()   , 50 say "30"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb26[i] picture "9999,999"
next i

@ prow()+2 , 1 say "8.2.3. Za zaposljavanje"
@ prow()   , 50 say "31"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb27[i] picture "9999,999"
next i

@ prow()+2 , 1 say " 9.   Placeni doprinosi za PIO za ben. staz"
@ prow()   , 50 say "32"


@ prow()+2 , 1 say "III   ZARADA ZA ISPLATU I DOPRINOSI ZA UPLATU"


@ prow()+2 , 1 say "10.   Zarada za uplatu (rb 1. minus rb 7.)"
@ prow()   , 50 say "33"
@ prow() , 41 + 16 say bb29[1] picture "9999,999"


@ prow()+2 , 1 say "11.   Doprinos za uplatu (rb 4. minus rb 8.)"
@ prow()   , 50 say "34"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb30[i] picture "9999,999"
next i


@ prow()+2 , 1 say "11.1. Od zaposlenih(rb. 4.1 minus 8.1 )"
@ prow()   , 50 say "35"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb31[i] picture "9999,999"
next i

@ prow()+2 , 1 say "11.1.1. Za PIO (4.1.1 minus 8.1.1.)"
@ prow()   , 50 say "36"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb32[i] picture "9999,999"
next i

@ prow()+2 , 1 say "11.1.2. Za zdravstvo (4.1.2 minus 8.1.2.)"
@ prow()   , 50 say "37"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb33[i] picture "9999,999"
next i

@ prow()+2 , 1 say "11.1.3. Za zaposljavanje (4.1.3 minus 8.1.3.)"
@ prow()   , 50 say "38"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb34[i] picture "9999,999"
next i

@ prow()+2 , 1 say "11.2.  Na teret posl.(rb. 4.2 minus 8.2)"
@ prow()   , 50 say "39"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb35[i] picture "9999,999"
next i

@ prow()+2 , 1 say "11.2.1. Za PIO"
@ prow()   , 50 say "40"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb36[i] picture "9999,999"
next i

@ prow()+2 , 1 say "11.2.2. Za zdravstvo"
@ prow()   , 50 say "41"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb37[i] picture "9999,999"
next i

@ prow()+2 , 1 say "11.2.3. Za zaposljavanje"
@ prow()   , 50 say "42"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb38[i] picture "9999,999"
next i

eject
@ prow() , 110 say "Strana 3"


@ prow()+2 , 1 say "12.    Doprinos za uplatu PIO za benef. staz"
@ prow()   , 50 say "43"


@ prow()+2 , 1 say "IV     VISE PLACENI DOPRINOSI "
@ prow()+1 , 1 say "       I UMANJENJE DOPRINOSA ZA UPLATU"



@ prow()+2 , 1 say "13    Vise placeni doprinosi (rb 13.1.+ 13.2.)"
@ prow()   , 50 say "44"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb40[i] picture "9999,999"
next i

@ prow()+2 , 1 say "13.1. Od zaposlenih(rb. 8.1 - 4.1)"
@ prow()   , 50 say "45"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb41[i] picture "9999,999"
next i

@ prow()+2 , 1 say "13.1.1.Za PIO (8.1.1 - 4.1.1)"
@ prow()   , 50 say "46"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb42[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.1.2.Za zdravstvo (8.1.2-4.1.2)"
@ prow()   , 50 say "47"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb43[i] picture "9999,999"
next i

@ prow()+2 , 1 say "13.1.3.Za zaposljavanje(8.1.3-4.1.3)"
@ prow()   , 50 say "48"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb44[i] picture "9999,999"
next i

@ prow()+2 , 1 say "13.2.  Na teret posl.(rb. 8.2 - 4.2)"
@ prow()   , 50 say "49"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb45[i] picture "9999,999"
next i

@ prow()+2 , 1 say "13.2.1.Za PIO (8.2.1 - 4.2.1)"
@ prow()   , 50 say "50"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb46[i] picture "9999,999"
next i

@ prow()+2 , 1 say "13.2.2.Za zdravstvo(8.2.2 - 4.2.2)"
@ prow()   , 50 say "51"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb47[i] picture "9999,999"
next i

@ prow()+2 , 1 say "13.2.3.Za zaposljavanje(8.2.3 - 4.2.3)"
@ prow()   , 50 say "52"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb48[i] picture "9999,999"
next i

@ prow()+2 , 1 say "  V    IZNOS DOPRINOSA ZA UPL PO UMANJ"


@ prow()+2 , 1 say "14    Doprinosi za uplatu (11-13 iz OD)"
@ prow()   , 50 say "53"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb49[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.1. Od zaposlenih (11.1-13.1 iz OD)"
@ prow()   , 50 say "54"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb50[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.1.1.Za PIO (11.1.1-13.1.1 iz OD)"
@ prow()   , 50 say "55"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb51[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.1.2.Za zdravstvo (11.1.2-13.1.2 iz OD)"
@ prow()   , 50 say "56"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb52[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.1.3.Za zaposljavanje (11.1.3-13.1.3 iz OD)"
@ prow()   , 50 say "57"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb53[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.2.  Na teret posl. (11.2-13.2 iz OD)"
@ prow()   , 50 say "58"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb54[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.2.1.Za PIO (11.2.1-13.2.1 iz OD)"
@ prow()   , 50 say "59"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb55[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.2.2.Za zdravstvo (11.2.2-13.2.2 iz OD)"
@ prow()   , 50 say "60"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb56[i] picture "9999,999"
next i

@ prow()+2 , 1 say "14.2.3.Za zaposljavanje (11.2.3-13.2.3 iz OD)"
@ prow()   , 50 say "61"
for i := 1 to 4 
   @ prow() , 41 + i*16 say bb57[i] picture "9999,999"
next i

@ prow()+4   ,1 say " Da su iskazani podaci u obrascu OD tacni, tvrdi i overava:"
@ prow()+2   ,1 say " U__________________________                                (M.P.)         ODGOVORNO LICE ISPLATIOCA"
@ prow()+2   ,1 say " Dana___________200____ god.                                               ___________________________"

@ prow()+2   ,1 say " Poresku prijavu popunio           "
@ prow()+1   ,1 say "________________________                                                   Kontrolu izvrsili:"
@ prow()+2   ,1 say "                                                                           1. _________________________"
@ prow()+2   ,1 say "PECAT PORESKE UPRAVE                                                       2. _________________________"

@ prow()+3   ,1 say "Razlika izmedju rb 4 i 11 od 10.905 odgovara iznosu doprinosa koje je Nacionalna sluzba zaposl uplatila"
@ prow()+1   ,1 say "i koji je iskayzan u obrascima INSZ, INSZ-P, INSZ-M i INSZ-I"
//@ prow()+5,2 say '"Sl. glasnik RS"  br. 90/02'

eject



close all

SET CONSOLE ON
set device to screen
next loop
return













procedure OPJ_obrazac()
local gar_zar
local zar_dop := 0
local akontacija
local kopija := 1
local isplata := 1 

local p_kolko[10]
local p_zarada[10]
local p_porez[10]
local p_fond_porez[10]
local p_za_ispl[10]

local ap_kolko[10]
local ap_zarada[10]
local ap_porez[10]
local ap_fond_porez[10]
local ap_za_ispl[10]

local porez := 0.1
local fond_porez := 0

local spec

cls 
@ 10,10 say "Broj kopija :" get kopija picture "999"
@ 12,10 say "Redni broj isplate :" get isplata picture "999"
read

 
for loop:= 1 to kopija

zar_dop := 0

afill(p_kolko, 0)
afill(p_zarada, 0)
afill(p_porez, 0)
afill(p_fond_porez, 0)
afill(p_za_ispl, 0)


afill(ap_kolko, 0)
afill(ap_zarada, 0)
afill(ap_porez, 0)
afill(ap_fond_porez, 0)
afill(ap_za_ispl, 0)

use porezi index porezi new
seek(1)
gar_zar:=porezi->zarada
fcasova:=porezi->fondcasova

akontacija := porezi->akont


close porezi

SET CONSOLE OFF
set device to printer

//    OBRACUN OPJ NA OBRACUN

use obracun index obracun new
go top

do while !eof()

   uk_neto := obracun->neto_zar + obracun->neto_nak + obracun->dodaci

if uk_neto > 1 
   
   p_kolko[1] := p_kolko[1] + 1

   if obracun->neto_zar <> 0          // zarade           1.
      p_zarada[1] := p_zarada[1] + obracun->neto_zar
      p_zarada[10] := p_zarada[10] + obracun->neto_zar
   endif

   if obracun->neto_nak <> 0          //  naknade         2.
      p_kolko[2] := p_kolko[2] + 1
      p_zarada[2] := p_zarada[2] + obracun->neto_nak      
      p_zarada[10] := p_zarada[10] + obracun->neto_nak      
   endif                                            

   if obracun->dodaci <> 0           //  dodaci na zaradu   3.
      p_kolko[3] := p_kolko[3] + 1
      p_zarada[3] := p_zarada[3] + obracun->dodaci
      p_zarada[10] := p_zarada[10] + obracun->dodaci
   endif

   if obracun->neto_TO <> 0           //  ishrana    3.1.
      p_kolko[4] := p_kolko[4] + 1
      p_zarada[4] := p_zarada[4] + obracun->neto_TO
   endif

   if .f. // obracun->dodaci <> 0           //  regres    3.2.
      p_kolko[5] := p_kolko[5] + 1
      p_zarada[5] := p_zarada[5] + obracun->dodaci
   endif

   if obracun->neto_ter <> 0           //  terenski    3.3.
      p_kolko[6] := p_kolko[6] + 1
      p_zarada[6] := p_zarada[6] + obracun->neto_ter
   endif

   if .f. //  obracun->dodaci <> 0           //  ostali dodaci    3.4.
      p_kolko[7] := p_kolko[7] + 1
      p_zarada[7] := p_zarada[7] + obracun->dodaci
   endif

   //  BB PO
   if obracun->umanjenje <> 0           //  umanjenje    3.3.
      p_kolko[8] := p_kolko[8] + 1
      p_zarada[8] := p_zarada[8] + obracun->umanjenje
   endif

   if obracun->porez_iz <> 0           //  iznos poreza    3.3.
      p_kolko[9] := p_kolko[9] + 1
      p_zarada[9] := p_zarada[9] + obracun->porez_iz
   endif


endif

   skip

enddo

close all


//    OBRACUN OPJ NA AKONTACIJU

use ak_obrac index ak_obrac new
go top

do while !eof()

   uk_neto := ak_obrac->neto_zar + ak_obrac->neto_nak + ak_obrac->dodaci

if uk_neto > 1 

   if ak_obrac->neto_zar <> 0          // zarade           1.
      ap_kolko[1] := ap_kolko[1] + 1
      ap_zarada[1] := ap_zarada[1] + ak_obrac->neto_zar
      ap_zarada[10] := ap_zarada[10] + ak_obrac->neto_zar
   endif

   if ak_obrac->neto_nak <> 0          //  naknade         2.
      ap_kolko[2] := ap_kolko[2] + 1
      ap_zarada[2] := ap_zarada[2] + ak_obrac->neto_nak      
      ap_zarada[10] := ap_zarada[10] + ak_obrac->neto_nak      
   endif                                            

   if ak_obrac->dodaci <> 0           //  dodaci na zaradu   3.
      ap_kolko[3] := ap_kolko[3] + 1
      ap_zarada[3] := ap_zarada[3] + ak_obrac->dodaci
      ap_zarada[10] := ap_zarada[10] + ak_obrac->dodaci
   endif

   if ak_obrac->neto_TO <> 0           //  ishrana    3.1.
      ap_kolko[4] := ap_kolko[4] + 1
      ap_zarada[4] := ap_zarada[4] + ak_obrac->neto_TO
   endif

   if .f. // ak_obrac->dodaci <> 0           //  regres    3.2.
      ap_kolko[5] := ap_kolko[5] + 1
      ap_zarada[5] := ap_zarada[5] + ak_obrac->dodaci
   endif

   if ak_obrac->neto_ter <> 0           //  terenski    3.3.
      ap_kolko[6] := ap_kolko[6] + 1
      ap_zarada[6] := ap_zarada[6] + ak_obrac->neto_ter
   endif

   if .f. //  ak_obrac->dodaci <> 0           //  ostali dodaci    3.4.
      ap_kolko[7] := ap_kolko[7] + 1
      ap_zarada[7] := ap_zarada[7] + ak_obrac->dodaci
   endif

   //  BB PO
   if ak_obrac->umanjenje <> 0           //  umanjenje    3.3.
      ap_kolko[8] := ap_kolko[8] + 1
      ap_zarada[8] := ap_zarada[8] + ak_obrac->umanjenje
   endif

   if ak_obrac->porez_iz <> 0           //  iznos poreza    3.3.
      ap_kolko[9] := ap_kolko[9] + 1
      ap_zarada[9] := ap_zarada[9] + ak_obrac->porez_iz
   endif


endif

   skip

enddo

close all


//   OBRACUN DOPRINOSA

use obracun index obracun new

for i:=1 to 9
sum &("dop_zar"+str(i,1,0)) + &("dop_bol"+str(i,1,0)) + &("dop_nak"+str(i,1,0));
    to &("d"+str(i,1,0))
next i

close obracun

use ak_obrac index ak_obrac new

for i:=1 to 9
sum &("dop_zar"+str(i,1,0)) + &("dop_bol"+str(i,1,0)) + &("dop_nak"+str(i,1,0));
    to &("ad"+str(i,1,0))
next i

sum_br := d1 + d2 + d3 + d4 + d5 + d6 + d7 + d8 + d9 
asum_br := ad1 + ad2 + ad3 + ad4 + ad5 + ad6 + ad7 + ad8 + ad9 

close all


mes1:= month(date())

do case
case mes1=1
  imemes1:=" januar"
case mes1=2
  imemes1:=" februar"
case mes1=3
  imemes1:=" mart"
case mes1=4
  imemes1:=" april"
case mes1=5
  imemes1:=" maj"
case mes1=6
  imemes1:=" jun"
case mes1=7
  imemes1:=" jul"
case mes1=8
  imemes1:=" avgust"
case mes1=9
  imemes1:=" septembar"
case mes1=10
  imemes1:=" oktobar"
case mes1=11
  imemes1:=" novembar"
case mes1=12
  imemes1:=" decembar"
endcase


@ prow(),1 say uska_slova

@ prow()+1,1 say "Isplatilac _________________________________"
@ prow(),14 say  rtrim(imekor)
@ prow()+1,1 say "Sediste isplatioca ___________________________________"
@ prow(),21 say rtrim(ul_i_br) + ", " + rtrim(br_mesto)
@ prow()+1,1 say "PIB _________________________________"
@ prow(),6 say  rtrim(PIB)
@ prow(),115 say dupla_slova + "PP OPJ"


@ prow()+1,90 say "     REPUBLIKA SRBIJA"
@ prow()+1,1 say "│   │   │   │   │   │   │   │   │  "
@ prow(),3 say razredjen(mat_broj,3)
@ prow(),90 say "  MINISTARSTVO FINANSIJA"
@ prow()+1,1 say "└───┴───┴───┴───┴───┴───┴───┴───┘  "
//@ prow(),90 say "      I EKONOMIJE"
@ prow()+1,1 say "         Maticni broj       "

@ prow(),90 say "    -PORESKA UPRAVA-"
@ prow()+1,1 say "Ziro racun isplatioca _____________________"
@ prow(),25 say rtrim(z_r)
@ prow(),90 say "- Organizaciona jedinica____________________"
@ prow()+1,1 say "Sifra delatnosti ispl. _________"
@ prow(),26 say  rtrim(sif_delat)

@ prow()+1,1 say "Uplatni racun poreza na zar: _____________________"
@ prow(),26 say  ""
@ prow(),30 say "840-711111843-52"
@ prow(),90 say "Potvrda o prijemu"


@ prow(),1 say siroka_slova
@ prow()+3,25 say "PORESKA PRIJAVA O OBRACUNATOM "
@ prow()+1,25 say " I PLACENOM POREZU NA ZARADE "

@ prow()+1,19 say "za mesec " + imemes + "(redni broj isplate  " + alltrim(str(isplata,3,0))+ ")" + str(god,5,0) + ". godine"
@ prow()+1,17 say "Isplata izvrsena ______________________"

@ prow(),1 say uska_slova

@ prow()+1 , 10 say "______________________________________________________________________________________________________"
@ prow()+1 , 10 say "                                                                      BROJ ZAPOSL.              IZNOS"
@ prow()+1 , 10 say "Rbr                O  P  I  S                                         ANGAZ LICA "
@ prow()+1 , 10 say " 1                      2                                                 3                         4"
@ prow()+1 , 10 say "______________________________________________________________________________________________________"
@ prow()+1 , 10 say "I. ZARADA I DRUGA PRIMANJA "
@ prow()+1 , 10 say "1.  Ispl. zarada zaposlenima-osnovica poreza (cl. 13 st. 1)......."
   @ prow(),85 say p_kolko[1] picture "999"
@ prow(),103 say (p_zarada[1]+p_zarada[2]+p_zarada[3]-ap_zarada[1]-ap_zarada[2]-ap_zarada[3]) picture "9,999,999"
@ prow()+1, 10 say "2.  Iznos zarade oslobodjen placanja poreza (2.1 + 2.2 + 2.3)........"
@ prow()+1, 10 say "2.1 Poresko oslobodjenje od ______din(cl.15 st.2)...................."
// BB PO
@ prow(),103   say  p_zarada[8]-ap_zarada[8]  picture "9,999,999"
@ prow()+1, 10 say "2.2 Premija dobrovoljnog PIO (clan 21a Zakona)......................."
@ prow()+1, 10 say "2.3 Zarada osoba sa invaliditetom zaposlenih u preduzecu za radno"
@ prow()+1, 10 say "     osposobljav. i zaposljavanje invalida (clan 21 Zakona) ........."
@ prow()+1, 10 say "3.  Oporezivi iznos zarade - osnovica poreza (1-2)................."
   @ prow(),85 say p_kolko[1] picture "999"
// BB PO
@ prow(),103 say (p_zarada[1]+p_zarada[2]+p_zarada[3]-ap_zarada[1]-ap_zarada[2]-ap_zarada[3]) - (p_zarada[8]-ap_zarada[8]) picture "9,999,999"
@ prow()+1, 10 say "4.  Ostala primanja zaposlenih - osn poreza (cl 14-14b Zakona)......."
@ prow()+1, 10 say "5.  Obracunati porez na zarade zaposlenih (3+4)x10% .........."
   @ prow(),85 say p_kolko[1] picture "999"
// BB PO
@ prow(),103 say (p_zarada[9] -ap_zarada[9])  picture "9,999,999"
@ prow()+1, 10 say "6.  Umanjenje obrac poreza po osnovu novozaposlenih radnika"
@ prow()+1, 10 say "       (clan 21b Zakona) ............................................"
@ prow()+1, 10 say "6.1 Umanjenje obracunatog poreza po cl 21v .........................."
@ prow()+1, 10 say "6.2 Umanjenje obracunatog poreza po cl 21g .........................."
@ prow()+1, 10 say "6.3 Umanjenje obracunatog poreza po cl 21d .........................."
@ prow()+1, 10 say "7   Porez na zarade placen u drugoj drzavi..........................."
@ prow()+1, 10 say "8.  Placeni porez na zarade zaposlenih (5-6-7) ......................"
   @ prow(),85 say p_kolko[1] picture "999"
// BB PO
@ prow(),103 say (p_zarada[9] -ap_zarada[9])  picture "9,999,999"
@ prow()+1, 10 say "II  UGOVORENA NAKNADA I DRUGA PRIMANJA"
@ prow()+1, 10 say "    ZA PRIVREMENE I POVREMENE POSLOVE"
@ prow()+1, 10 say "9.  Zarada - ugovorena naknada i druga primanja za privremene"
@ prow()+1, 10 say "     i povremene poslove - osnovica poreza (cl. 13 st. 2. Zakona)...."
@ prow()+1, 10 say "10. Obracunati i placeni porez na zarade za privremene i povremene"
@ prow()+1, 10 say "      poslove (10x ___%)............................................."
@ prow()+1, 10 say "11. Isplacena licna zarada preduzetnika (cl. 13 st 3)................"
@ prow()+1, 10 say "12. Iznos licne zarade oslobodjen placanja poreza(12.1+12.2)........."
@ prow()+1, 10 say "12.1 Poresko oslobodjenje od ________ din. (cl. 15 st 2.)............"
@ prow()+1, 10 say "12.2 Premija za dobrovoljno zdravst. osig., odnosno penz. doprinos"
@ prow()+1, 10 say "      u dobrovoljni penzijski fond (cl. 21a)........................."
@ prow()+1, 10 say "13  Oporezivi iznos licne zarade - osovica poreza (11-12)............"
@ prow()+1, 10 say "14  Obracunati porez na licnu zaradu preduzetnika (13x___%).........."
@ prow()+1, 10 say "IV UKUPNO PLACENI POREZ"
@ prow()+1, 10 say "15. Ukupno placeni porez na zarade(8+10+14).........................."
   @ prow(),85 say p_kolko[1] picture "999"
// BB PO
@ prow(),103 say (p_zarada[9] -ap_zarada[9]) picture "9,999,999"

@ prow()+3,21 say  "U Pirotu, dana __________200__. godine"
@ prow()+2,21 say  "Da su iskazani podaci u ovoj poreskoj prijavi tacni tvrdi i overava:"
@ prow()+2,21 say  "Poresku prijavu popunio                 Odg. lice isplatioca        Poresku pr. u Poreskoj"
@ prow()+2,18 say "   ___________________        (M.P.)       __________________          upravi kontrolisali"
@ prow()+2,18 say "                                                                     1.__________________"
@ prow()+2,18 say "                                                                     2.__________________"

//@ prow()+10,2 say '"Sl. glasnik RS"  br. 90/02'

eject

set device to screen
spec:="DA"
@ 12,10 say "Stampa specifikacije :" get spec picture "999"
read
if upper(spec) == "DA"
set device to PRINTER
@ prow(),1 say siroka_slova
@ prow()+3,25 say "SPECIFIKACIJA UZ PORESKU PRIJAVU PP OPJ"
@ prow()+3,15 say " PIB ISPLATIOCA:    " + rtrim(PIB)

@ prow(),1 say uska_slova
@ prow()+1 , 10 say "______________________________________________________________________________________________________"
@ prow()+1 , 10 say "                                                                     POREZ NA              "
@ prow()+1 , 10 say "Rbr                SIFRA OPSTINE           NAZIV OPSTINE          DOHODAK GRADJANA"
@ prow()+1 , 10 say " 1                      2                       3                         4                         "
@ prow()+1 , 10 say "______________________________________________________________________________________________________"
@ prow()+2 , 10 say " 1                    079                    PIROT"
@ prow(),85 say (p_zarada[9] - ap_zarada[9] ) picture "999,999"
@ prow()+1 , 10 say "______________________________________________________________________________________________________"
@ prow()+2 , 10 say " 2                                                "
@ prow()+1 , 10 say "______________________________________________________________________________________________________"
@ prow()+1 , 10 say "______________________________________________________________________________________________________"
@ prow()+2 , 10 say "                     UKUPNO                       "
@ prow(),85 say (p_zarada[9] - ap_zarada[9] ) picture "999,999"
@ prow()+1 , 10 say "______________________________________________________________________________________________________"
eject

endif

close all

SET CONSOLE ON
set device to screen

next loop

return


