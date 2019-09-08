ˇZ
2Y:\Develop\EntityGraphQL\src\dotnet-gql\Program.cs
	namespace 	

dotnet_gql
 
{ 
public 

class 
Program 
{ 
[ 	
Argument	 
( 
$num 
, 
Description  
=! "
$str# h
)h i
]i j
[ 	
Required	 
] 
public 
string 
ContextClass "
{# $
get% (
;( )
}* +
[ 	
Option	 
( 
	ShortName 
= 
$str 
,  
Description! ,
=- .
$str/ P
)P Q
]Q R
public 
string 
	Namespace 
{  !
get" %
;% &
}' (
=) *
$str+ :
;: ;
[ 	
Option	 
( 
	ShortName 
= 
$str 
,  
Description! ,
=- .
$str	/ Ä
)
Ä Å
]
Å Ç
public 
string 
Project 
{ 
get  #
;# $
}% &
=' (
$str) ,
;, -
[ 	
Option	 
( 
LongName 
= 
$str &
,& '
	ShortName( 1
=2 3
$str4 7
,7 8
Description9 D
=E F
$strG ]
)] ^
]^ _
public 
string 
OutputClassName %
{& '
get( +
;+ ,
}- .
=/ 0
$str1 G
;G H
[ 	
Option	 
( 
LongName 
= 
$str #
,# $
	ShortName% .
=/ 0
$str1 4
,4 5
Description6 A
=B C
$strD U
)U V
]V W
public 
string 
OutputFilename $
{% &
get' *
;* +
}, -
=. /
$str0 B
;B C
public   
static   
int   
Main   
(   
string   %
[  % &
]  & '
args  ( ,
)  , -
=>  . 0"
CommandLineApplication  1 G
.  G H
Execute  H O
<  O P
Program  P W
>  W X
(  X Y
args  Y ]
)  ] ^
;  ^ _
private"" 
async"" 
void"" 
	OnExecute"" $
(""$ %
)""% &
{## 	
try$$ 
{%% 
Console&& 
.&& 
	WriteLine&& !
(&&! "
$"&&" $
	Building &&$ -
{&&- .
Project&&. 5
}&&5 6
...&&6 9
"&&9 :
)&&: ;
;&&; <
var(( 
	buildProc(( 
=(( 
System((  &
.((& '
Diagnostics((' 2
.((2 3
Process((3 :
.((: ;
Start((; @
(((@ A
$str((A I
,((I J
$"((K M
build ((M S
{((S T
Project((T [
}(([ \
"((\ ]
)((] ^
;((^ _
	buildProc)) 
.)) 
WaitForExit)) %
())% &
)))& '
;))' (
Console++ 
.++ 
	WriteLine++ !
(++! "
$"++" $
Loading class ++$ 2
{++2 3
ContextClass++3 ?
}++? @
 from ++@ F
{++F G
Project++G N
}++N O
"++O P
)++P Q
;++Q R
var,, 
contextType,, 
=,,  !
LoadContextClass,," 2
(,,2 3
),,3 4
;,,4 5

Expression00 
<00 
Func00 
<00  
ISchemaProvider00  /
>00/ 0
>000 1
call002 6
=007 8
(009 :
)00: ;
=>00< >
SchemaBuilder00? L
.00L M

FromObject00M W
<00W X
object00X ^
>00^ _
(00_ `
true00` d
)00d e
;00e f
var11 
method11 
=11 
(11 
(11  
MethodCallExpression11 3
)113 4
call114 8
.118 9
Body119 =
)11= >
.11> ?
Method11? E
;11E F
method22 
=22 
method22 
.22  &
GetGenericMethodDefinition22  :
(22: ;
)22; <
.22< =
MakeGenericMethod22= N
(22N O
contextType22O Z
)22Z [
;22[ \
var33 
schema33 
=33 
method33 #
.33# $
Invoke33$ *
(33* +
null33+ /
,33/ 0
new331 4
object335 ;
[33; <
]33< =
{33> ?
true33? C
}33C D
)33D E
as33F H
ISchemaProvider33I X
;33X Y
Console55 
.55 
	WriteLine55 !
(55! "
$"55" $
Generating 55$ /
{55/ 0
	Namespace550 9
}559 :
.55: ;
{55; <
OutputClassName55< K
}55K L
, outputting to 55L \
{55\ ]
OutputFilename55] k
}55k l
"55l m
)55m n
;55n o
var88 
engine88 
=88 
new88  #
RazorLightEngineBuilder88! 8
(888 9
)889 :
.99 '
UseEmbeddedResourcesProject99 0
(990 1
typeof991 7
(997 8
Program998 ?
)99? @
)99@ A
.:: $
UseMemoryCachingProvider:: -
(::- .
)::. /
.;; 
Build;; 
(;; 
);; 
;;; 
string== 
result== 
=== 
await==  %
engine==& ,
.==, -
CompileRenderAsync==- ?
(==? @
$str==@ Q
,==Q R
new==S V
{==W X
	Namespace>> 
=>> 
	Namespace>>  )
,>>) *
OutputClassName?? #
=??$ %
OutputClassName??& 5
,??5 6
ContextClass@@  
=@@! "
ContextClass@@# /
,@@/ 0
SchemaAA 
=AA 
schemaAA #
}BB 
)BB 
;BB 
FileCC 
.CC 
WriteAllTextCC !
(CC! "
OutputFilenameCC" 0
,CC0 1
resultCC2 8
)CC8 9
;CC9 :
}DD 
catchEE 
(EE 
	ExceptionEE 
eEE 
)EE 
{FF 
ConsoleGG 
.GG 
	WriteLineGG !
(GG! "
$strGG" +
+GG, -
eGG. /
.GG/ 0
ToStringGG0 8
(GG8 9
)GG9 :
)GG: ;
;GG; <
}HH 
}II 	
privateKK 
TypeKK 
LoadContextClassKK %
(KK% &
)KK& '
{LL 	
stringNN 
projFileNN 
=NN 
nullNN "
;NN" #
stringOO 
projPathOO 
=OO 
nullOO "
;OO" #
ifPP 
(PP 
FilePP 
.PP 
ExistsPP 
(PP 
ProjectPP #
)PP# $
)PP$ %
{QQ 
projFileRR 
=RR 
ProjectRR "
;RR" #
projPathSS 
=SS 
PathSS 
.SS  
GetDirectoryNameSS  0
(SS0 1
ProjectSS1 8
)SS8 9
;SS9 :
}TT 
elseUU 
{VV 
projFileXX 
=XX 
	DirectoryXX $
.XX$ %
GetFilesXX% -
(XX- .
ProjectXX. 5
,XX5 6
$strXX7 >
)XX> ?
.XX? @
FirstOrDefaultXX@ N
(XXN O
)XXO P
;XXP Q
projPathYY 
=YY 
ProjectYY "
;YY" #
ifZZ 
(ZZ 
projFileZZ 
==ZZ 
nullZZ  $
)ZZ$ %
{[[ 
throw\\ 
new\\ 
ArgumentException\\ /
(\\/ 0
$"\\0 2/
#Could not find csproj file in path \\2 U
{\\U V
Project\\V ]
}\\] ^
"\\^ _
)\\_ `
;\\` a
}]] 
}^^ 
var`` 
xml`` 
=`` 
new`` 
XmlDocument`` %
(``% &
)``& '
;``' (
xmlaa 
.aa 
Loadaa 
(aa 
newaa 

FileStreamaa #
(aa# $
projFileaa$ ,
,aa, -
FileModeaa. 6
.aa6 7
Openaa7 ;
)aa; <
)aa< =
;aa= >
varbb 
assemblyNamebb 
=bb 
xmlbb "
.bb" # 
GetElementsByTagNamebb# 7
(bb7 8
$strbb8 F
)bbF G
.bbG H
CountbbH M
>bbN O
$numbbP Q
?bbR S
xmlbbT W
.bbW X 
GetElementsByTagNamebbX l
(bbl m
$strbbm {
)bb{ |
.bb| }
Item	bb} Å
(
bbÅ Ç
$num
bbÇ É
)
bbÉ Ñ
.
bbÑ Ö
	InnerText
bbÖ é
:
bbè ê
Path
bbë ï
.
bbï ñ)
GetFileNameWithoutExtension
bbñ ±
(
bb± ≤
projFile
bb≤ ∫
)
bb∫ ª
;
bbª º
varcc 
targetFrameworkcc 
=cc  !
xmlcc" %
.cc% & 
GetElementsByTagNamecc& :
(cc: ;
$strcc; L
)ccL M
.ccM N
ItemccN R
(ccR S
$numccS T
)ccT U
.ccU V
	InnerTextccV _
;cc_ `
vardd 
assemblyPathdd 
=dd 
$"dd !
{dd! "
Pathdd" &
.dd& '
GetFullPathdd' 2
(dd2 3
projPathdd3 ;
)dd; <
}dd< =
/bin/Debug/dd= H
{ddH I
targetFrameworkddI X
}ddX Y
/ddY Z
{ddZ [
assemblyNamedd[ g
}ddg h
.dllddh l
"ddl m
;ddm n
ifff 
(ff 
!ff 
Fileff 
.ff 
Existsff 
(ff 
assemblyPathff )
)ff) *
)ff* +
{gg 
throwhh 
newhh 
ArgumentExceptionhh +
(hh+ ,
$"hh, .1
%Could not find assembly. Looking for hh. S
{hhS T
assemblyPathhhT `
}hh` a
"hha b
)hhb c
;hhc d
}ii 
Consolejj 
.jj 
	WriteLinejj 
(jj 
$"jj  "
Loading assembly from jj  6
{jj6 7
assemblyPathjj7 C
}jjC D
"jjD E
)jjE F
;jjF G
varll 
pluginll 
=ll 
PluginLoaderll %
.ll% &"
CreateFromAssemblyFilell& <
(ll< =
assemblyPathll= I
)llI J
;llJ K
varmm 
assemblymm 
=mm 
pluginmm !
.mm! "
LoadAssemblymm" .
(mm. /
assemblyNamemm/ ;
)mm; <
;mm< =
varnn 
typenn 
=nn 
assemblynn 
.nn  
GetTypenn  '
(nn' (
ContextClassnn( 4
)nn4 5
;nn5 6
returnoo 
typeoo 
;oo 
}pp 	
}qq 
}rr 