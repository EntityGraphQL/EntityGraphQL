�
:Y:\Develop\EntityGraphQL\src\EntityGraphQL\AssemblyInfo.cs
[ 
assembly 	
:	 

CLSCompliant 
( 
false 
) 
] �
JY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\CompiledQueryResult.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
public 

class 
CompiledQueryResult $
{ 
private 
readonly 
List 
< 
ParameterExpression 1
>1 2
contextParams3 @
;@ A
public 
LambdaExpression 
LambdaExpression  0
{1 2
get3 6
{7 8
return9 ?

Expression@ J
.J K
LambdaK Q
(Q R
ExpressionResultR b
.b c

Expressionc m
,m n
ContextParamso |
.| }
Concat	} �
(
� �
ExpressionResult
� �
.
� � 
ConstantParameters
� �
.
� �
Keys
� �
)
� �
.
� �
ToArray
� �
(
� �
)
� �
)
� �
;
� �
}
� �
}
� �
public 
IReadOnlyDictionary "
<" #
ParameterExpression# 6
,6 7
object8 >
>> ?
ConstantParameters@ R
{S T
getU X
{Y Z
return[ a
ExpressionResultb r
.r s
ConstantParameters	s �
;
� �
}
� �
}
� �
public 
Type 
BodyType 
{ 
get "
{# $
return% +
LambdaExpression, <
.< =
Body= A
.A B
TypeB F
;F G
}H I
}J K
public 
ExpressionResult 
ExpressionResult  0
{1 2
get3 6
;6 7
private8 ?
set@ C
;C D
}E F
public 
bool 

IsMutation 
{  
get! $
{% &
return' -
typeof. 4
(4 5
MutationResult5 C
)C D
==E G
ExpressionResultH X
.X Y
GetTypeY `
(` a
)a b
;b c
}d e
}f g
public 
List 
< 
ParameterExpression '
>' (
ContextParams) 6
=>7 9
contextParams: G
;G H
public 
CompiledQueryResult "
(" #
ExpressionResult# 3
expressionResult4 D
,D E
ListF J
<J K
ParameterExpressionK ^
>^ _
contextParams` m
)m n
{ 	
this   
.   
ExpressionResult   !
=  " #
expressionResult  $ 4
;  4 5
this!! 
.!! 
contextParams!! 
=!!  
contextParams!!! .
;!!. /
}"" 	
public## 
object## 
Execute## 
(## 
params## $
object##% +
[##+ ,
]##, -
args##. 2
)##2 3
{$$ 	
var%% 
allArgs%% 
=%% 
new%% 
List%% "
<%%" #
object%%# )
>%%) *
(%%* +
args%%+ /
)%%/ 0
;%%0 1
if&& 
(&& 
ConstantParameters&& "
!=&&# %
null&&& *
)&&* +
{'' 
allArgs(( 
.(( 
AddRange((  
(((  !
ConstantParameters((! 3
.((3 4
Values((4 :
)((: ;
;((; <
})) 
return** 
LambdaExpression** #
.**# $
Compile**$ +
(**+ ,
)**, -
.**- .
DynamicInvoke**. ;
(**; <
allArgs**< C
.**C D
ToArray**D K
(**K L
)**L M
)**M N
;**N O
}++ 	
},, 
}-- �
UY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\EntityGraphQLCompilerException.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
public 

class *
EntityGraphQLCompilerException /
:0 1
System2 8
.8 9
	Exception9 B
{ 
public *
EntityGraphQLCompilerException -
(- .
string. 4
message5 <
)< =
:> ?
base@ D
(D E
messageE L
)L M
{ 	
} 	
} 
}		 �8
BY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\EqlCompiler.cs
	namespace		 	
EntityGraphQL		
 
.		 
Compiler		  
{

 
public 

static 
class 
EqlCompiler #
{ 
public 
static 
CompiledQueryResult )
Compile* 1
(1 2
string2 8
query9 >
)> ?
{ 	
return 
Compile 
( 
query  
,  !
null" &
,& '
new( +!
DefaultMethodProvider, A
(A B
)B C
,C D
nullE I
)I J
;J K
} 	
public 
static 
CompiledQueryResult )
Compile* 1
(1 2
string2 8
query9 >
,> ?
ISchemaProvider@ O
schemaProviderP ^
)^ _
{ 	
return 
Compile 
( 
query  
,  !
schemaProvider" 0
,0 1
new2 5!
DefaultMethodProvider6 K
(K L
)L M
,M N
nullO S
)S T
;T U
} 	
public'' 
static'' 
CompiledQueryResult'' )
Compile''* 1
(''1 2
string''2 8
query''9 >
,''> ?
ISchemaProvider''@ O
schemaProvider''P ^
,''^ _
IMethodProvider''` o
methodProvider''p ~
,''~ 
QueryVariables
''� �
	variables
''� �
)
''� �
{(( 	
ParameterExpression)) 
contextParam))  ,
=))- .
null))/ 3
;))3 4
if++ 
(++ 
schemaProvider++ 
!=++ !
null++" &
)++& '
contextParam,, 
=,, 

Expression,, )
.,,) *
	Parameter,,* 3
(,,3 4
schemaProvider,,4 B
.,,B C
ContextType,,C N
),,N O
;,,O P
var-- 

expression-- 
=-- 
CompileQuery-- )
(--) *
query--* /
,--/ 0
contextParam--1 =
,--= >
schemaProvider--? M
,--M N
methodProvider--O ]
,--] ^
	variables--_ h
)--h i
;--i j
var// 
contextParams// 
=// 
new//  #
List//$ (
<//( )
ParameterExpression//) <
>//< =
(//= >
)//> ?
;//? @
if00 
(00 
contextParam00 
!=00 
null00  $
)00$ %
contextParams11 
.11 
Add11 !
(11! "
contextParam11" .
)11. /
;11/ 0
return22 
new22 
CompiledQueryResult22 *
(22* +

expression22+ 5
,225 6
contextParams227 D
)22D E
;22E F
}33 	
public55 
static55 
CompiledQueryResult55 )
CompileWith55* 5
(555 6
string556 <
query55= B
,55B C

Expression55D N
context55O V
,55V W
ISchemaProvider55X g
schemaProvider55h v
,55v w
IMethodProvider	55x �
methodProvider
55� �
=
55� �
null
55� �
,
55� �
QueryVariables
55� �
	variables
55� �
=
55� �
null
55� �
)
55� �
{66 	
if77 
(77 
methodProvider77 
==77 !
null77" &
)77& '
{88 
methodProvider99 
=99  
new99! $!
DefaultMethodProvider99% :
(99: ;
)99; <
;99< =
}:: 
if;; 
(;; 
	variables;; 
==;; 
null;; !
);;! "
{<< 
	variables== 
=== 
new== 
QueryVariables==  .
(==. /
)==/ 0
;==0 1
}>> 
var?? 

expression?? 
=?? 
CompileQuery?? )
(??) *
query??* /
,??/ 0
context??1 8
,??8 9
schemaProvider??: H
,??H I
methodProvider??J X
,??X Y
	variables??Z c
)??c d
;??d e
varAA 

parametersAA 
=AA 

expressionAA '
.AA' (

ExpressionAA( 2
.AA2 3
NodeTypeAA3 ;
==AA< >
ExpressionTypeAA? M
.AAM N
LambdaAAN T
?AAU V
(AAW X
(AAX Y
LambdaExpressionAAY i
)AAi j

expressionAAj t
.AAt u

ExpressionAAu 
)	AA �
.
AA� �

Parameters
AA� �
.
AA� �
ToList
AA� �
(
AA� �
)
AA� �
:
AA� �
new
AA� �
List
AA� �
<
AA� �!
ParameterExpression
AA� �
>
AA� �
(
AA� �
)
AA� �
;
AA� �
returnBB 
newBB 
CompiledQueryResultBB *
(BB* +

expressionBB+ 5
,BB5 6

parametersBB7 A
)BBA B
;BBB C
}CC 	
privateEE 
staticEE 
ExpressionResultEE '
CompileQueryEE( 4
(EE4 5
stringEE5 ;
queryEE< A
,EEA B

ExpressionEEC M
contextEEN U
,EEU V
ISchemaProviderEEW f
schemaProviderEEg u
,EEu v
IMethodProvider	EEw �
methodProvider
EE� �
,
EE� �
QueryVariables
EE� �
	variables
EE� �
)
EE� �
{FF 	
AntlrInputStreamGG 
streamGG #
=GG$ %
newGG& )
AntlrInputStreamGG* :
(GG: ;
queryGG; @
)GG@ A
;GGA B
varHH 
lexerHH 
=HH 
newHH 
EntityGraphQLLexerHH .
(HH. /
streamHH/ 5
)HH5 6
;HH6 7
varII 
tokensII 
=II 
newII 
CommonTokenStreamII .
(II. /
lexerII/ 4
)II4 5
;II5 6
varJJ 
parserJJ 
=JJ 
newJJ 
EntityGraphQLParserJJ 0
(JJ0 1
tokensJJ1 7
)JJ7 8
;JJ8 9
parserKK 
.KK 
BuildParseTreeKK !
=KK" #
trueKK$ (
;KK( )
varLL 
treeLL 
=LL 
parserLL 
.LL 
	startRuleLL '
(LL' (
)LL( )
;LL) *
varNN 
visitorNN 
=NN 
newNN #
QueryGrammerNodeVisitorNN 5
(NN5 6
contextNN6 =
,NN= >
schemaProviderNN? M
,NNM N
methodProviderNNO ]
,NN] ^
	variablesNN_ h
)NNh i
;NNi j
varOO 

expressionOO 
=OO 
visitorOO $
.OO$ %
VisitOO% *
(OO* +
treeOO+ /
)OO/ 0
;OO0 1
returnPP 

expressionPP 
;PP 
}QQ 	
}RR 
}SS �
GY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\ExpressionResult.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
public 

class 
ExpressionResult !
{ 
private 

Dictionary 
< 
ParameterExpression .
,. /
object0 6
>6 7
constantParameters8 J
=K L
newM P

DictionaryQ [
<[ \
ParameterExpression\ o
,o p
objectq w
>w x
(x y
)y z
;z {
public 
ExpressionResult 
(  

Expression  *
value+ 0
)0 1
{ 	
this 
. 

Expression 
= 
value #
;# $
} 	
public 
virtual 

Expression !

Expression" ,
{- .
get/ 2
;2 3
internal4 <
set= @
;@ A
}B C
public 
Type 
Type 
{ 
get 
{  
return! '

Expression( 2
.2 3
Type3 7
;7 8
}9 :
}; <
public 
IReadOnlyDictionary "
<" #
ParameterExpression# 6
,6 7
object8 >
>> ?
ConstantParameters@ R
{S T
getU X
=>Y [
constantParameters\ n
;n o
}p q
public 
ExpressionType 
NodeType &
{' (
get) ,
{- .
return/ 5

Expression6 @
.@ A
NodeTypeA I
;I J
}K L
}M N
public 
static 
implicit 
operator '

Expression( 2
(2 3
ExpressionResult3 C
fieldD I
)I J
{ 	
return 
field 
. 

Expression #
;# $
} 	
public%% 
static%% 
explicit%% 
operator%% '
ExpressionResult%%( 8
(%%8 9

Expression%%9 C
value%%D I
)%%I J
{&& 	
return'' 
new'' 
ExpressionResult'' '
(''' (
value''( -
)''- .
;''. /
}(( 	
internal** 
void**  
AddConstantParameter** *
(*** +
ParameterExpression**+ >
type**? C
,**C D
object**E K
value**L Q
)**Q R
{++ 	
constantParameters,, 
.,, 
Add,, "
(,," #
type,,# '
,,,' (
value,,) .
),,. /
;,,/ 0
}-- 	
internal// 
void// !
AddConstantParameters// +
(//+ ,
IReadOnlyDictionary//, ?
<//? @
ParameterExpression//@ S
,//S T
object//U [
>//[ \
constantParameters//] o
)//o p
{00 	
foreach11 
(11 
var11 
item11 
in11  
constantParameters11! 3
)113 4
{22  
AddConstantParameter33 $
(33$ %
item33% )
.33) *
Key33* -
,33- .
item33/ 3
.333 4
Value334 9
)339 :
;33: ;
}44 
}55 	
}66 
}77 �=
FY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\GraphQLCompiler.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{		 
public

 

class

 
GraphQLCompiler

  
{ 
private 
ISchemaProvider 
_schemaProvider  /
;/ 0
private 
IMethodProvider 
_methodProvider  /
;/ 0
public 
GraphQLCompiler 
( 
ISchemaProvider .
schemaProvider/ =
,= >
IMethodProvider? N
methodProviderO ]
)] ^
{ 	
_schemaProvider 
= 
schemaProvider ,
;, -
_methodProvider 
= 
methodProvider ,
;, -
} 	
public 
GraphQLResultNode  
Compile! (
(( )
string) /
query0 5
,5 6
QueryVariables7 E
	variablesF O
=P Q
nullR V
)V W
{   	
if!! 
(!! 
	variables!! 
==!! 
null!! !
)!!! "
{"" 
	variables## 
=## 
new## 
QueryVariables##  .
(##. /
)##/ 0
;##0 1
}$$ 
return%% 
Compile%% 
(%% 
new%% 
QueryRequest%% +
{%%, -
Query%%- 2
=%%3 4
query%%5 :
,%%: ;
	Variables%%< E
=%%F G
	variables%%H Q
}%%Q R
)%%R S
;%%S T
}&& 	
public'' 
GraphQLResultNode''  
Compile''! (
(''( )
QueryRequest'') 5
request''6 =
)''= >
{(( 	
var** 
stream** 
=** 
new** 
AntlrInputStream** -
(**- .
request**. 5
.**5 6
Query**6 ;
)**; <
;**< =
var++ 
lexer++ 
=++ 
new++ 
EntityGraphQLLexer++ .
(++. /
stream++/ 5
)++5 6
;++6 7
var,, 
tokens,, 
=,, 
new,, 
CommonTokenStream,, .
(,,. /
lexer,,/ 4
),,4 5
;,,5 6
var-- 
parser-- 
=-- 
new-- 
EntityGraphQLParser-- 0
(--0 1
tokens--1 7
)--7 8
;--8 9
parser.. 
... 
BuildParseTree.. !
=.." #
true..$ (
;..( )
parser// 
.// 
ErrorHandler// 
=//  !
new//" %
BailErrorStrategy//& 7
(//7 8
)//8 9
;//9 :
try00 
{11 
var22 
tree22 
=22 
parser22 !
.22! "
graphQL22" )
(22) *
)22* +
;22+ ,
var33 
visitor33 
=33 
new33 !
GraphQLVisitor33" 0
(330 1
_schemaProvider331 @
,33@ A
_methodProvider33B Q
,33Q R
request33S Z
.33Z [
	Variables33[ d
)33d e
;33e f
var55 
node55 
=55 
visitor55 "
.55" #
Visit55# (
(55( )
tree55) -
)55- .
;55. /
return66 
(66 
GraphQLResultNode66 )
)66) *
node66* .
;66. /
}77 
catch88 
(88 "
ParseCanceledException88 )
pce88* -
)88- .
{99 
if:: 
(:: 
pce:: 
.:: 
InnerException:: &
!=::' )
null::* .
)::. /
{;; 
if<< 
(<< 
pce<< 
.<< 
InnerException<< *
is<<+ - 
NoViableAltException<<. B
)<<B C
{== 
var>> 
nve>> 
=>>  !
(>>" # 
NoViableAltException>># 7
)>>7 8
pce>>8 ;
.>>; <
InnerException>>< J
;>>J K
throw?? 
new?? !*
EntityGraphQLCompilerException??" @
(??@ A
$"??A C
Error: line ??C O
{??O P
nve??P S
.??S T
OffendingToken??T b
.??b c
Line??c g
}??g h
:??h i
{??i j
nve??j m
.??m n
OffendingToken??n |
.??| }
Column	??} �
}
??� �/
! no viable alternative at input '
??� �
{
??� �
nve
??� �
.
??� �
OffendingToken
??� �
.
??� �
Text
??� �
}
??� �
'
??� �
"
??� �
)
??� �
;
??� �
}@@ 
elseAA 
ifAA 
(AA 
pceAA  
.AA  !
InnerExceptionAA! /
isAA0 2"
InputMismatchExceptionAA3 I
)AAI J
{BB 
varCC 
imeCC 
=CC  !
(CC" #"
InputMismatchExceptionCC# 9
)CC9 :
pceCC: =
.CC= >
InnerExceptionCC> L
;CCL M
varDD 
	expectingDD %
=DD& '
stringDD( .
.DD. /
JoinDD/ 3
(DD3 4
$strDD4 8
,DD8 9
imeDD: =
.DD= >
GetExpectedTokensDD> O
(DDO P
)DDP Q
)DDQ R
;DDR S
throwEE 
newEE !*
EntityGraphQLCompilerExceptionEE" @
(EE@ A
$"EEA C
Error: line EEC O
{EEO P
imeEEP S
.EES T
OffendingTokenEET b
.EEb c
LineEEc g
}EEg h
:EEh i
{EEi j
imeEEj m
.EEm n
OffendingTokenEEn |
.EE| }
Column	EE} �
}
EE� �!
 extraneous input '
EE� �
{
EE� �
ime
EE� �
.
EE� �
OffendingToken
EE� �
.
EE� �
Text
EE� �
}
EE� �
' expecting 
EE� �
{
EE� �
	expecting
EE� �
}
EE� �
"
EE� �
)
EE� �
;
EE� �
}FF 
SystemGG 
.GG 
ConsoleGG "
.GG" #
	WriteLineGG# ,
(GG, -
pceGG- 0
.GG0 1
InnerExceptionGG1 ?
.GG? @
GetTypeGG@ G
(GGG H
)GGH I
)GGI J
;GGJ K
throwHH 
newHH *
EntityGraphQLCompilerExceptionHH <
(HH< =
pceHH= @
.HH@ A
InnerExceptionHHA O
.HHO P
MessageHHP W
)HHW X
;HHX Y
}II 
throwJJ 
newJJ *
EntityGraphQLCompilerExceptionJJ 8
(JJ8 9
pceJJ9 <
.JJ< =
MessageJJ= D
)JJD E
;JJE F
}KK 
}LL 	
}MM 
publicOO 

classOO 
SchemaExceptionOO  
:OO! "
	ExceptionOO# ,
{PP 
publicQQ 
SchemaExceptionQQ 
(QQ 
stringQQ %
messageQQ& -
)QQ- .
:QQ/ 0
baseQQ1 5
(QQ5 6
messageQQ6 =
)QQ= >
{QQ? @
}QQA B
publicRR 
staticRR 
SchemaExceptionRR %!
MakeFieldCompileErrorRR& ;
(RR; <
stringRR< B
queryRRC H
,RRH I
stringRRJ P
messageRRQ X
)RRX Y
{SS 	
returnTT 
newTT 
SchemaExceptionTT &
(TT& '
$"TT' )#
Error compiling query 'TT) @
{TT@ A
queryTTA F
}TTF G
'. TTG J
{TTJ K
messageTTK R
}TTR S
"TTS T
)TTT U
;TTU V
}UU 	
}VV 
}WW �
FY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\GraphQLFragment.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
public 

class 
GraphQLFragment  
{ 
private 
string 
TypeName 
{  !
get" %
;% &
}' (
public

 
string

 
Name

 
{

 
get

  
;

  !
}

" #
public 
IEnumerable 
< 
IGraphQLBaseNode +
>+ ,
Fields- 3
{4 5
get6 9
;9 :
}; <
public 
ParameterExpression "
SelectContext# 0
{1 2
get3 6
;6 7
}8 9
public 
GraphQLFragment 
( 
string %
name& *
,* +
string, 2
typeName3 ;
,; <
IEnumerable= H
<H I
IGraphQLBaseNodeI Y
>Y Z
fields[ a
,a b
ParameterExpressionc v
selectContext	w �
)
� �
{ 	
Name 
= 
name 
; 
TypeName 
= 
typeName 
;  
Fields 
= 
fields 
; 
SelectContext 
= 
selectContext )
;) *
} 	
} 
public 

class !
GraphQLFragmentSelect &
:' (
IGraphQLBaseNode) 9
{ 
private 
string 
name 
; 
public   !
GraphQLFragmentSelect   $
(  $ %
string  % +
name  , 0
)  0 1
{!! 	
this"" 
."" 
name"" 
="" 
name"" 
;"" 
}## 	
public%% 
string%% 
Name%% 
=>%% 
this%% "
.%%" #
name%%# '
;%%' (
public'' 
OperationType'' 
Type'' !
=>''" $
OperationType''% 2
.''2 3
Fragment''3 ;
;''; <
}(( 
})) �U
JY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\GraphQLMutationNode.cs
	namespace		 	
EntityGraphQL		
 
.		 
Compiler		  
{

 
public 

class 
GraphQLMutationNode $
:% &
IGraphQLNode' 3
{ 
private 
CompiledQueryResult #
result$ *
;* +
private 
IGraphQLNode 
graphQLNode (
;( )
public 
IEnumerable 
< 
IGraphQLNode '
>' (
Fields) /
{0 1
get2 5
;5 6
private7 >
set? B
;B C
}D E
public 
string 
Name 
=> 
graphQLNode )
.) *
Name* .
;. /
public 
OperationType 
Type !
=>" $
OperationType% 2
.2 3
Mutation3 ;
;; <
public 
IReadOnlyDictionary "
<" #
ParameterExpression# 6
,6 7
object8 >
>> ?
ConstantParameters@ R
=>S U
nullV Z
;Z [
public 
List 
< 
ParameterExpression '
>' (

Parameters) 3
=>4 6
throw7 <
new= @#
NotImplementedExceptionA X
(X Y
)Y Z
;Z [
public 
ExpressionResult 
NodeExpression  .
{/ 0
get1 4
=>5 7
throw8 =
new> A#
NotImplementedExceptionB Y
(Y Z
)Z [
;[ \
set] `
=>a c
throwd i
newj m$
NotImplementedException	n �
(
� �
)
� �
;
� �
}
� �
public 
GraphQLMutationNode "
(" #
CompiledQueryResult# 6
result7 =
,= >
IGraphQLNode? K
graphQLNodeL W
)W X
{ 	
this 
. 
result 
= 
result  
;  !
this 
. 
graphQLNode 
= 
graphQLNode *
;* +
Fields 
= 
new 
List 
< 
IGraphQLNode *
>* +
(+ ,
), -
;- .
}   	
public"" 
object"" 
Execute"" 
("" 
params"" $
object""% +
[""+ ,
]"", -
args"". 2
)""2 3
{## 	
var$$ 
allArgs$$ 
=$$ 
new$$ 
List$$ "
<$$" #
object$$# )
>$$) *
($$* +
args$$+ /
)$$/ 0
;$$0 1
var'' 
mutation'' 
='' 
('' 
MutationResult'' *
)''* +
this''+ /
.''/ 0
result''0 6
.''6 7
ExpressionResult''7 G
;''G H
var(( 
result(( 
=(( 
mutation(( !
.((! "
Execute((" )
((() *
args((* .
)((. /
;((/ 0
if)) 
()) 
result)) 
.)) 
GetType)) 
()) 
)))  
.))  !
GetTypeInfo))! ,
()), -
)))- .
.)). /
BaseType))/ 7
.))7 8
GetTypeInfo))8 C
())C D
)))D E
.))E F
BaseType))F N
==))O Q
typeof))R X
())X Y
LambdaExpression))Y i
)))i j
)))j k
{** 
var++ 
mutationLambda++ "
=++# $
(++% &
LambdaExpression++& 6
)++6 7
result++7 =
;++= >
var,,  
mutationContextParam,, (
=,,) *
mutationLambda,,+ 9
.,,9 :

Parameters,,: D
.,,D E
First,,E J
(,,J K
),,K L
;,,L M
var-- 
mutationExpression-- &
=--' (
mutationLambda--) 7
.--7 8
Body--8 <
;--< =
var88 
selectParam88 
=88  !
graphQLNode88" -
.88- .

Parameters88. 8
.888 9
First889 >
(88> ?
)88? @
;88@ A
if:: 
(:: 
!:: 
mutationLambda:: #
.::# $

ReturnType::$ .
.::. /
IsEnumerableOrArray::/ B
(::B C
)::C D
&&::E G
mutationExpression::H Z
.::Z [
NodeType::[ c
==::d f
ExpressionType::g u
.::u v
Call::v z
)::z {
{;; 
var<< 
call<< 
=<< 
(<<   
MethodCallExpression<<  4
)<<4 5
mutationExpression<<5 G
;<<G H
if== 
(== 
call== 
.== 
Method== #
.==# $
Name==$ (
====) +
$str==, 3
||==4 6
call==7 ;
.==; <
Method==< B
.==B C
Name==C G
====H J
$str==K [
||==\ ^
call==_ c
.==c d
Method==d j
.==j k
Name==k o
====p r
$str==s y
||==z |
call	==} �
.
==� �
Method
==� �
.
==� �
Name
==� �
==
==� �
$str
==� �
)
==� �
{>> 
var?? 
baseExp?? #
=??$ %
call??& *
.??* +
	Arguments??+ 4
.??4 5
First??5 :
(??: ;
)??; <
;??< =
if@@ 
(@@ 
call@@  
.@@  !
	Arguments@@! *
.@@* +
Count@@+ 0
(@@0 1
)@@1 2
==@@3 5
$num@@6 7
)@@7 8
{AA 
varCC 
filterCC  &
=CC' (
callCC) -
.CC- .
	ArgumentsCC. 7
.CC7 8
	ElementAtCC8 A
(CCA B
$numCCB C
)CCC D
;CCD E
baseExpDD #
=DD$ %

ExpressionDD& 0
.DD0 1
CallDD1 5
(DD5 6
typeofDD6 <
(DD< =
	QueryableDD= F
)DDF G
,DDG H
$strDDI P
,DDP Q
newDDR U
TypeDDV Z
[DDZ [
]DD[ \
{DD] ^
selectParamDD_ j
.DDj k
TypeDDk o
}DDp q
,DDq r
callDDs w
.DDw x
	Arguments	DDx �
.
DD� �
First
DD� �
(
DD� �
)
DD� �
,
DD� �
filter
DD� �
)
DD� �
;
DD� �
}EE 
varHH 
	selectExpHH %
=HH& '

ExpressionHH( 2
.HH2 3
CallHH3 7
(HH7 8
typeofHH8 >
(HH> ?
	QueryableHH? H
)HHH I
,HHI J
$strHHK S
,HHS T
newHHU X
TypeHHY ]
[HH] ^
]HH^ _
{HH` a
selectParamHHb m
.HHm n
TypeHHn r
,HHr s
graphQLNodeHHt 
.	HH �
NodeExpression
HH� �
.
HH� �
Type
HH� �
}
HH� �
,
HH� �
baseExp
HH� �
,
HH� �

Expression
HH� �
.
HH� �
Lambda
HH� �
(
HH� �
graphQLNode
HH� �
.
HH� �
NodeExpression
HH� �
,
HH� �
selectParam
HH� �
)
HH� �
)
HH� �
;
HH� �
varKK 
firstExpKK $
=KK% &

ExpressionKK' 1
.KK1 2
CallKK2 6
(KK6 7
typeofKK7 =
(KK= >
	QueryableKK> G
)KKG H
,KKH I
callKKJ N
.KKN O
MethodKKO U
.KKU V
NameKKV Z
,KKZ [
newKK\ _
TypeKK` d
[KKd e
]KKe f
{KKg h
	selectExpKKi r
.KKr s
TypeKKs w
.KKw x 
GetGenericArguments	KKx �
(
KK� �
)
KK� �
[
KK� �
$num
KK� �
]
KK� �
}
KK� �
,
KK� �
	selectExp
KK� �
)
KK� �
;
KK� �
graphQLNodeNN #
.NN# $
NodeExpressionNN$ 2
=NN3 4
(NN5 6
ExpressionResultNN6 F
)NNF G
firstExpNNG O
;NNO P
}OO 
elsePP 
{QQ 
throwRR 
newRR !
QueryExceptionRR" 0
(RR0 1
$"RR1 3
	Mutation RR3 <
{RR< =
NameRR= A
}RRA B(
 has invalid return type of RRB ^
{RR^ _
resultRR_ e
.RRe f
GetTypeRRf m
(RRm n
)RRn o
}RRo pr
e. Please return Expression<Func<TConext, TEntity>> or Expression<Func<TConext, IEnumerable<TEntity>>>	RRp �
"
RR� �
)
RR� �
;
RR� �
}SS 
}TT 
elseUU 
{VV 
varWW 
expWW 
=WW 

ExpressionWW (
.WW( )
CallWW) -
(WW- .
typeofWW. 4
(WW4 5
	QueryableWW5 >
)WW> ?
,WW? @
$strWWA I
,WWI J
newWWK N
TypeWWO S
[WWS T
]WWT U
{WWV W
selectParamWWX c
.WWc d
TypeWWd h
,WWh i
graphQLNodeWWj u
.WWu v
NodeExpression	WWv �
.
WW� �
Type
WW� �
}
WW� �
,
WW� � 
mutationExpression
WW� �
,
WW� �

Expression
WW� �
.
WW� �
Lambda
WW� �
(
WW� �
graphQLNode
WW� �
.
WW� �
NodeExpression
WW� �
,
WW� �
selectParam
WW� �
)
WW� �
)
WW� �
;
WW� �
graphQLNodeXX 
.XX  
NodeExpressionXX  .
=XX/ 0
(XX1 2
ExpressionResultXX2 B
)XXB C
expXXC F
;XXF G
}YY 
graphQLNode\\ 
.\\ 

Parameters\\ &
[\\& '
$num\\' (
]\\( )
=\\* + 
mutationContextParam\\, @
;\\@ A
result]] 
=]] 
graphQLNode]] $
.]]$ %
Execute]]% ,
(]], -
args]]- 1
[]]1 2
$num]]2 3
]]]3 4
)]]4 5
;]]5 6
return^^ 
result^^ 
;^^ 
}__ 
resultaa 
=aa 
graphQLNodeaa  
.aa  !
Executeaa! (
(aa( )
resultaa) /
)aa/ 0
;aa0 1
returnbb 
resultbb 
;bb 
}cc 	
}dd 
}ee ��
BY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\GraphQLNode.cs
	namespace		 	
EntityGraphQL		
 
.		 
Compiler		  
{

 
public 

class 
GraphQLNode 
: 
IGraphQLNode +
{ 
private 
readonly 
ISchemaProvider (
schemaProvider) 7
;7 8
private 
readonly 
IEnumerable $
<$ %
IGraphQLBaseNode% 5
>5 6
fieldSelection7 E
;E F
private   
readonly   
ParameterExpression   ,
fieldParameter  - ;
;  ; <
private$$ 
readonly$$ 
IEnumerable$$ $
<$$$ %
GraphQLFragment$$% 4
>$$4 5
queryFragments$$6 D
;$$D E
private(( 
ExpressionResult((  
nodeExpression((! /
;((/ 0
private,, 
ExpressionResult,,  (
fieldSelectionBaseExpression,,! =
;,,= >
private-- 
List-- 
<-- 
IGraphQLNode-- !
>--! "

nodeFields--# -
;--- .
private.. 

Dictionary.. 
<.. 
ParameterExpression.. .
,... /
object..0 6
>..6 7
constantParameters..8 J
;..J K
public00 
string00 
Name00 
{00 
get00  
;00  !
private00" )
set00* -
;00- .
}00/ 0
public11 
OperationType11 
Type11 !
=>11" $
OperationType11% 2
.112 3
Query113 8
;118 9
public:: 
ExpressionResult:: 
NodeExpression::  .
{;; 	
get<< 
{== 
if@@ 
(@@ 
nodeExpression@@ "
==@@# %
null@@& *
&&@@+ -
fieldSelection@@. <
!=@@= ?
null@@@ D
&&@@E G
fieldSelection@@H V
.@@V W
Any@@W Z
(@@Z [
)@@[ \
)@@\ ]
{AA 
varBB 
replacerBB  
=BB! "
newBB# &
ParameterReplacerBB' 8
(BB8 9
)BB9 :
;BB: ;
varCC 
selectionFieldsCC '
=CC( )
newCC* -
ListCC. 2
<CC2 3
IGraphQLNodeCC3 ?
>CC? @
(CC@ A
)CCA B
;CCB C
boolDD 
isSelectDD !
=DD" #(
fieldSelectionBaseExpressionDD$ @
.DD@ A
TypeDDA E
.DDE F
IsEnumerableOrArrayDDF Y
(DDY Z
)DDZ [
;DD[ \
foreachFF 
(FF 
varFF  
fieldFF! &
inFF' )
fieldSelectionFF* 8
)FF8 9
{GG 
ifHH 
(HH 
fieldHH !
isHH" $!
GraphQLFragmentSelectHH% :
)HH: ;
{II 
varJJ 
fragmentJJ  (
=JJ) *
queryFragmentsJJ+ 9
.JJ9 :
FirstOrDefaultJJ: H
(JJH I
iJJI J
=>JJK M
iJJN O
.JJO P
NameJJP T
==JJU W
fieldJJX ]
.JJ] ^
NameJJ^ b
)JJb c
;JJc d
ifKK 
(KK  
fragmentKK  (
==KK) +
nullKK, 0
)KK0 1
throwLL  %
newLL& )"
EntityQuerySchemaErrorLL* @
(LL@ A
$"LLA C

Fragment 'LLC M
{LLM N
fieldLLN S
.LLS T
NameLLT X
}LLX Y)
' not found in query documentLLY v
"LLv w
)LLw x
;LLx y
foreachNN #
(NN$ %
IGraphQLNodeNN% 1
	fragFieldNN2 ;
inNN< >
fragmentNN? G
.NNG H
FieldsNNH N
)NNN O
{OO 
ExpressionResultPP  0
expPP1 4
=PP5 6
nullPP7 ;
;PP; <
ifQQ  "
(QQ# $
isSelectQQ$ ,
)QQ, -
expRR$ '
=RR( )
(RR* +
ExpressionResultRR+ ;
)RR; <
replacerRR< D
.RRD E
ReplaceRRE L
(RRL M
	fragFieldRRM V
.RRV W
NodeExpressionRRW e
,RRe f
fragmentRRg o
.RRo p
SelectContextRRp }
,RR} ~
fieldParameter	RR �
)
RR� �
;
RR� �
elseSS  $
expTT$ '
=TT( )
(TT* +
ExpressionResultTT+ ;
)TT; <
replacerTT< D
.TTD E
ReplaceTTE L
(TTL M
	fragFieldTTM V
.TTV W
NodeExpressionTTW e
,TTe f
fragmentTTg o
.TTo p
SelectContextTTp }
,TT} ~)
fieldSelectionBaseExpression	TT �
)
TT� �
;
TT� �
selectionFieldsVV  /
.VV/ 0
AddVV0 3
(VV3 4
newVV4 7
GraphQLNodeVV8 C
(VVC D
schemaProviderVVD R
,VVR S
queryFragmentsVVT b
,VVb c
	fragFieldVVd m
.VVm n
NameVVn r
,VVr s
expVVt w
,VVw x
nullVVy }
,VV} ~
null	VV �
,
VV� �
null
VV� �
,
VV� �
null
VV� �
)
VV� �
)
VV� �
;
VV� �
foreachYY  '
(YY( )
varYY) ,
itemYY- 1
inYY2 4
	fragFieldYY5 >
.YY> ?
ConstantParametersYY? Q
)YYQ R
{ZZ  !
constantParameters[[$ 6
.[[6 7
Add[[7 :
([[: ;
item[[; ?
.[[? @
Key[[@ C
,[[C D
item[[E I
.[[I J
Value[[J O
)[[O P
;[[P Q
}\\  !
}]] 
}^^ 
else__ 
{`` 
varaa 
gfieldaa  &
=aa' (
(aa) *
IGraphQLNodeaa* 6
)aa6 7
fieldaa7 <
;aa< =
selectionFieldsbb +
.bb+ ,
Addbb, /
(bb/ 0
gfieldbb0 6
)bb6 7
;bb7 8
foreachdd #
(dd$ %
vardd% (
itemdd) -
indd. 0
gfielddd1 7
.dd7 8
ConstantParametersdd8 J
)ddJ K
{ee 
constantParametersff  2
.ff2 3
Addff3 6
(ff6 7
itemff7 ;
.ff; <
Keyff< ?
,ff? @
itemffA E
.ffE F
ValueffF K
)ffK L
;ffL M
}gg 
}hh 
}ii 
ifjj 
(jj 
isSelectjj  
)jj  !
{kk 
nodeExpressionmm &
=mm' (
(mm) *
ExpressionResultmm* :
)mm: ;
ExpressionUtilmm; I
.mmI J
SelectDynamicToListmmJ ]
(mm] ^
fieldParametermm^ l
,mml m)
fieldSelectionBaseExpression	mmn �
,
mm� �
selectionFields
mm� �
,
mm� �
schemaProvider
mm� �
)
mm� �
;
mm� �
}nn 
elseoo 
{pp 
varrr 
newExprr "
=rr# $
ExpressionUtilrr% 3
.rr3 4
CreateNewExpressionrr4 G
(rrG H(
fieldSelectionBaseExpressionrrH d
,rrd e
selectionFieldsrrf u
,rru v
schemaProvider	rrw �
)
rr� �
;
rr� �
varss 
anonTypess $
=ss% &
newExpss' -
.ss- .
Typess. 2
;ss2 3
newExpuu 
=uu  

Expressionuu! +
.uu+ ,
	Conditionuu, 5
(uu5 6

Expressionuu6 @
.uu@ A

MakeBinaryuuA K
(uuK L
ExpressionTypeuuL Z
.uuZ [
Equaluu[ `
,uu` a(
fieldSelectionBaseExpressionuub ~
,uu~ 

Expression
uu� �
.
uu� �
Constant
uu� �
(
uu� �
null
uu� �
)
uu� �
)
uu� �
,
uu� �

Expression
uu� �
.
uu� �
Constant
uu� �
(
uu� �
null
uu� �
,
uu� �
anonType
uu� �
)
uu� �
,
uu� �
newExp
uu� �
,
uu� �
anonType
uu� �
)
uu� �
;
uu� �
nodeExpressionvv &
=vv' (
(vv) *
ExpressionResultvv* :
)vv: ;
newExpvv; A
;vvA B
}ww 
foreachxx 
(xx 
varxx  
fieldxx! &
inxx' )
selectionFieldsxx* 9
)xx9 :
{yy 
foreachzz 
(zz  !
varzz! $
cpzz% '
inzz( *
fieldzz+ 0
.zz0 1
ConstantParameterszz1 C
)zzC D
{{{ 
if|| 
(||  
!||  !
constantParameters||! 3
.||3 4
ContainsKey||4 ?
(||? @
cp||@ B
.||B C
Key||C F
)||F G
)||G H
{}} 
constantParameters~~  2
.~~2 3
Add~~3 6
(~~6 7
cp~~7 9
.~~9 :
Key~~: =
,~~= >
cp~~? A
.~~A B
Value~~B G
)~~G H
;~~H I
} 
}
�� 
}
�� 
foreach
�� 
(
�� 
var
��  
item
��! %
in
��& (*
fieldSelectionBaseExpression
��) E
.
��E F 
ConstantParameters
��F X
)
��X Y
{
��  
constantParameters
�� *
.
��* +
Add
��+ .
(
��. /
item
��/ 3
.
��3 4
Key
��4 7
,
��7 8
item
��9 =
.
��= >
Value
��> C
)
��C D
;
��D E
}
�� 
}
�� 
return
�� 
nodeExpression
�� %
;
��% &
}
�� 
set
�� 
=>
�� 
nodeExpression
�� !
=
��" #
value
��$ )
;
��) *
}
�� 	
public
�� 
List
�� 
<
�� !
ParameterExpression
�� '
>
��' (

Parameters
��) 3
{
��4 5
get
��6 9
;
��9 :
private
��; B
set
��C F
;
��F G
}
��H I
public
�� !
IReadOnlyDictionary
�� "
<
��" #!
ParameterExpression
��# 6
,
��6 7
object
��8 >
>
��> ? 
ConstantParameters
��@ R
{
��S T
get
��U X
=>
��Y [ 
constantParameters
��\ n
;
��n o
}
��p q
public
�� 
IEnumerable
�� 
<
�� 
IGraphQLNode
�� '
>
��' (
Fields
��) /
{
��0 1
get
��2 5
=>
��6 8

nodeFields
��9 C
;
��C D
}
��E F
public
�� 
GraphQLNode
�� 
(
�� 
ISchemaProvider
�� *
schemaProvider
��+ 9
,
��9 :
IEnumerable
��; F
<
��F G
GraphQLFragment
��G V
>
��V W
queryFragments
��X f
,
��f g
string
��h n
name
��o s
,
��s t"
CompiledQueryResult��u �
query��� �
,��� � 
ExpressionResult��� �,
fieldSelectionBaseExpression��� �
)��� �
:��� �
this��� �
(��� �
schemaProvider��� �
,��� �
queryFragments��� �
,��� �
name��� �
,��� �
(��� � 
ExpressionResult��� �
)��� �
query��� �
.��� � 
ExpressionResult��� �
,��� �,
fieldSelectionBaseExpression��� �
,��� �
query��� �
.��� � 
LambdaExpression��� �
.��� �

Parameters��� �
,��� �
null��� �
,��� �
null��� �
)��� �
{
�� 	
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��  
query
��! &
.
��& ' 
ConstantParameters
��' 9
)
��9 :
{
��  
constantParameters
�� "
.
��" #
Add
��# &
(
��& '
item
��' +
.
��+ ,
Key
��, /
,
��/ 0
item
��1 5
.
��5 6
Value
��6 ;
)
��; <
;
��< =
}
�� 
}
�� 	
public
�� 
GraphQLNode
�� 
(
�� 
ISchemaProvider
�� *
schemaProvider
��+ 9
,
��9 :
IEnumerable
��; F
<
��F G
GraphQLFragment
��G V
>
��V W
queryFragments
��X f
,
��f g
string
��h n
name
��o s
,
��s t
ExpressionResult��u �
exp��� �
,��� � 
ExpressionResult��� �,
fieldSelectionBaseExpression��� �
,��� �
IEnumerable��� �
<��� �#
ParameterExpression��� �
>��� �$
expressionParameters��� �
,��� �
IEnumerable��� �
<��� � 
IGraphQLBaseNode��� �
>��� �
fieldSelection��� �
,��� �#
ParameterExpression��� �
fieldParameter��� �
)��� �
{
�� 	
if
�� 
(
�� *
fieldSelectionBaseExpression
�� ,
==
��- /
null
��0 4
&&
��5 7
fieldSelection
��8 F
!=
��G I
null
��J N
)
��N O
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 8
(
��8 9
$"
��9 ;j
[fieldSelectionBaseExpression must be supplied for GraphQLNode if fieldSelection is supplied��; �
"��� �
)��� �
;��� �
Name
�� 
=
�� 
name
�� 
;
�� 
NodeExpression
�� 
=
�� 
exp
��  
;
��  !

nodeFields
�� 
=
�� 
new
�� 
List
�� !
<
��! "
IGraphQLNode
��" .
>
��. /
(
��/ 0
)
��0 1
;
��1 2
this
�� 
.
�� 
schemaProvider
�� 
=
��  !
schemaProvider
��" 0
;
��0 1
this
�� 
.
�� 
queryFragments
�� 
=
��  !
queryFragments
��" 0
;
��0 1
this
�� 
.
�� 
fieldSelection
�� 
=
��  !
fieldSelection
��" 0
;
��0 1
this
�� 
.
�� 
fieldParameter
�� 
=
��  !
fieldParameter
��" 0
;
��0 1
this
�� 
.
�� *
fieldSelectionBaseExpression
�� -
=
��. /*
fieldSelectionBaseExpression
��0 L
;
��L M

Parameters
�� 
=
�� "
expressionParameters
�� -
?
��- .
.
��. /
ToList
��/ 5
(
��5 6
)
��6 7
;
��7 8 
constantParameters
�� 
=
��  
new
��! $

Dictionary
��% /
<
��/ 0!
ParameterExpression
��0 C
,
��C D
object
��E K
>
��K L
(
��L M
)
��M N
;
��N O
if
�� 
(
�� 

Parameters
�� 
==
�� 
null
�� "
)
��" #
{
�� 

Parameters
�� 
=
�� 
new
��  
List
��! %
<
��% &!
ParameterExpression
��& 9
>
��9 :
(
��: ;
)
��; <
;
��< =
}
�� 
}
�� 	
public
�� 
object
�� 
Execute
�� 
(
�� 
params
�� $
object
��% +
[
��+ ,
]
��, -
args
��. 2
)
��2 3
{
�� 	
var
�� 
allArgs
�� 
=
�� 
new
�� 
List
�� "
<
��" #
object
��# )
>
��) *
(
��* +
args
��+ /
)
��/ 0
;
��0 1
var
�� 

expression
�� 
=
�� 
NodeExpression
�� +
;
��+ ,
if
�� 
(
�� 

expression
�� 
.
�� 
Type
�� 
.
��  !
IsEnumerableOrArray
��  3
(
��3 4
)
��4 5
)
��5 6
{
�� 

expression
�� 
=
�� 
ExpressionUtil
�� +
.
��+ , 
MakeExpressionCall
��, >
(
��> ?
new
��? B
[
��C D
]
��D E
{
��F G
typeof
��G M
(
��M N
	Queryable
��N W
)
��W X
,
��X Y
typeof
��Z `
(
��` a

Enumerable
��a k
)
��k l
}
��l m
,
��m n
$str
��o w
,
��w x
new
��y |
Type��} �
[��� �
]��� �
{��� �

expression��� �
.��� �
Type��� �
.��� �(
GetEnumerableOrArrayType��� �
(��� �
)��� �
}��� �
,��� �

expression��� �
)��� �
;��� �
}
�� 
var
�� 

parameters
�� 
=
�� 

Parameters
�� '
.
��' (
ToList
��( .
(
��. /
)
��/ 0
;
��0 1
if
�� 
(
��  
ConstantParameters
�� "
!=
��# %
null
��& *
&&
��+ - 
ConstantParameters
��. @
.
��@ A
Any
��A D
(
��D E
)
��E F
)
��F G
{
�� 

parameters
�� 
.
�� 
AddRange
�� #
(
��# $ 
ConstantParameters
��$ 6
.
��6 7
Keys
��7 ;
)
��; <
;
��< =
allArgs
�� 
.
�� 
AddRange
��  
(
��  ! 
ConstantParameters
��! 3
.
��3 4
Values
��4 :
)
��: ;
;
��; <
}
�� 
var
�� 
lambdaExpression
��  
=
��! "

Expression
��# -
.
��- .
Lambda
��. 4
(
��4 5

expression
��5 ?
,
��? @

parameters
��A K
.
��K L
ToArray
��L S
(
��S T
)
��T U
)
��U V
;
��V W
return
�� 
lambdaExpression
�� #
.
��# $
Compile
��$ +
(
��+ ,
)
��, -
.
��- .
DynamicInvoke
��. ;
(
��; <
allArgs
��< C
.
��C D
ToArray
��D K
(
��K L
)
��L M
)
��M N
;
��N O
}
�� 	
public
�� 
void
�� #
AddConstantParameters
�� )
(
��) *!
IReadOnlyDictionary
��* =
<
��= >!
ParameterExpression
��> Q
,
��Q R
object
��S Y
>
��Y Z 
constantParameters
��[ m
)
��m n
{
�� 	
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��   
constantParameters
��! 3
)
��3 4
{
�� 
this
�� 
.
��  
constantParameters
�� '
.
��' (
Add
��( +
(
��+ ,
item
��, 0
.
��0 1
Key
��1 4
,
��4 5
item
��6 :
.
��: ;
Value
��; @
)
��@ A
;
��A B
}
�� 
}
�� 	
public
�� 
override
�� 
string
�� 
ToString
�� '
(
��' (
)
��( )
{
�� 	
return
�� 
$"
�� 
Node - Name=
�� !
{
��! "
Name
��" &
}
��& '
, Expression=
��' 4
{
��4 5
NodeExpression
��5 C
}
��C D
"
��D E
;
��E F
}
�� 	
public
�� 
void
�� 
AddField
�� 
(
�� 
IGraphQLNode
�� )
node
��* .
)
��. /
{
�� 	

nodeFields
�� 
.
�� 
Add
�� 
(
�� 
node
�� 
)
��  
;
��  !
if
�� 
(
�� 
node
�� 
.
��  
ConstantParameters
�� '
!=
��( *
null
��+ /
)
��/ 0
{
�� 
foreach
�� 
(
�� 
var
�� 
item
�� !
in
��" $
node
��% )
.
��) * 
ConstantParameters
��* <
)
��< =
{
��  
constantParameters
�� &
.
��& '
Add
��' *
(
��* +
item
��+ /
.
��/ 0
Key
��0 3
,
��3 4
item
��5 9
.
��9 :
Value
��: ?
)
��? @
;
��@ A
}
�� 
}
�� 
}
�� 	
}
�� 
}�� �
HY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\GraphQLResultNode.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
public 

class 
GraphQLResultNode "
:# $
IGraphQLBaseNode% 5
{ 
private 
List 
< 
GraphQLFragment $
>$ %
	fragments& /
;/ 0
public$$ 
List$$ 
<$$ 
IGraphQLNode$$  
>$$  !

Operations$$" ,
{$$- .
get$$/ 2
;$$2 3
}$$4 5
public%% 
OperationType%% 
Type%% !
=>%%" $
OperationType%%% 2
.%%2 3
Result%%3 9
;%%9 :
public'' 
GraphQLResultNode''  
(''  !
IEnumerable''! ,
<'', -
IGraphQLNode''- 9
>''9 :

operations''; E
,''E F
List''G K
<''K L
GraphQLFragment''L [
>''[ \
	fragments''] f
)''f g
{(( 	
this)) 
.)) 

Operations)) 
=)) 

operations)) (
.))( )
ToList))) /
())/ 0
)))0 1
;))1 2
this** 
.** 
	fragments** 
=** 
	fragments** &
;**& '
}++ 	
public-- 
string-- 
Name-- 
=>-- 
$str-- 2
;--2 3
public66 
QueryResult66 
ExecuteQuery66 '
(66' (
object66( .
context66/ 6
,666 7
string668 >
operationName66? L
=66M N
null66O S
,66S T
params66U [
object66\ b
[66b c
]66c d
mutationArgs66e q
)66q r
{77 	
var88 
result88 
=88 
new88 
QueryResult88 (
(88( )
)88) *
;88* +
var99 
op99 
=99 
string99 
.99 
IsNullOrEmpty99 )
(99) *
operationName99* 7
)997 8
?999 :

Operations99; E
.99E F
First99F K
(99K L
)99L M
:99N O

Operations99P Z
.99Z [
First99[ `
(99` a
o99a b
=>99c e
o99f g
.99g h
Name99h l
==99m o
operationName99p }
)99} ~
;99~ 
foreach@@ 
(@@ 
var@@ 
node@@ 
in@@  
op@@! #
.@@# $
Fields@@$ *
)@@* +
{AA 
resultBB 
.BB 
DataBB 
[BB 
nodeBB  
.BB  !
NameBB! %
]BB% &
=BB' (
nullBB) -
;BB- .
varDD 
argsDD 
=DD 
newDD 
ListDD #
<DD# $
objectDD$ *
>DD* +
{DD, -
contextDD- 4
}DD4 5
;DD5 6
ifEE 
(EE 
nodeEE 
.EE 
TypeEE 
==EE  
OperationTypeEE! .
.EE. /
MutationEE/ 7
)EE7 8
{FF 
argsGG 
.GG 
AddRangeGG !
(GG! "
mutationArgsGG" .
)GG. /
;GG/ 0
}HH 
varII 
dataII 
=II 
nodeII 
.II  
ExecuteII  '
(II' (
argsII( ,
.II, -
ToArrayII- 4
(II4 5
)II5 6
)II6 7
;II7 8
resultJJ 
.JJ 
DataJJ 
[JJ 
nodeJJ  
.JJ  !
NameJJ! %
]JJ% &
=JJ' (
dataJJ) -
;JJ- .
}KK 
returnMM 
resultMM 
;MM 
}NN 	
}OO 
}PP ��
EY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\GraphQLVisitor.cs
	namespace

 	
EntityGraphQL


 
.

 
Compiler

  
{ 
internal 
class 
GraphQLVisitor !
:" #$
EntityGraphQLBaseVisitor$ <
<< =
IGraphQLBaseNode= M
>M N
{ 
private 
ISchemaProvider 
schemaProvider  .
;. /
private 
IMethodProvider 
methodProvider  .
;. /
private 
readonly 
QueryVariables '
	variables( 1
;1 2
private 

Expression 
selectContext (
;( )
private 
BaseIdentityFinder "
baseIdentityFinder# 5
=6 7
new8 ;
BaseIdentityFinder< N
(N O
)O P
;P Q
private 
List 
< 
GraphQLFragment $
>$ %
	fragments& /
=0 1
new2 5
List6 :
<: ;
GraphQLFragment; J
>J K
(K L
)L M
;M N
private   
List   
<   
IGraphQLNode   !
>  ! "
rootQueries  # .
=  / 0
new  1 4
List  5 9
<  9 :
IGraphQLNode  : F
>  F G
(  G H
)  H I
;  I J
public"" 
GraphQLVisitor"" 
("" 
ISchemaProvider"" -
schemaProvider"". <
,""< =
IMethodProvider""> M
methodProvider""N \
,""\ ]
QueryVariables""^ l
	variables""m v
)""v w
{## 	
this$$ 
.$$ 
schemaProvider$$ 
=$$  !
schemaProvider$$" 0
;$$0 1
this%% 
.%% 
methodProvider%% 
=%%  !
methodProvider%%" 0
;%%0 1
this&& 
.&& 
	variables&& 
=&& 
	variables&& &
;&&& '
}'' 	
public)) 
override)) 
IGraphQLBaseNode)) (

VisitField))) 3
())3 4
EntityGraphQLParser))4 G
.))G H
FieldContext))H T
context))U \
)))\ ]
{** 	
var++ 
name++ 
=++ 
baseIdentityFinder++ )
.++) *
Visit++* /
(++/ 0
context++0 7
)++7 8
;++8 9
var,, 
result,, 
=,, 
EqlCompiler,, $
.,,$ %
CompileWith,,% 0
(,,0 1
context,,1 8
.,,8 9
GetText,,9 @
(,,@ A
),,A B
,,,B C
selectContext,,D Q
,,,Q R
schemaProvider,,S a
,,,a b
methodProvider,,c q
,,,q r
	variables,,s |
),,| }
;,,} ~
var-- 

actualName-- 
=-- 
schemaProvider-- +
.--+ ,
GetActualFieldName--, >
(--> ?
schemaProvider--? M
.--M N(
GetSchemaTypeNameForRealType--N j
(--j k
selectContext--k x
.--x y
Type--y }
)--} ~
,--~ 
name
--� �
)
--� �
;
--� �
var.. 
node.. 
=.. 
new.. 
GraphQLNode.. &
(..& '
schemaProvider..' 5
,..5 6
	fragments..7 @
,..@ A

actualName..B L
,..L M
result..N T
,..T U
null..V Z
)..Z [
;..[ \
return// 
node// 
;// 
}00 	
public11 
override11 
IGraphQLBaseNode11 (
VisitAliasExp11) 6
(116 7
EntityGraphQLParser117 J
.11J K
AliasExpContext11K Z
context11[ b
)11b c
{22 	
var33 
name33 
=33 
context33 
.33 
alias33 $
.33$ %
name33% )
.33) *
GetText33* 1
(331 2
)332 3
;333 4
var44 
query44 
=44 
context44 
.44  
entity44  &
.44& '
GetText44' .
(44. /
)44/ 0
;440 1
if55 
(55 
selectContext55 
==55  
null55! %
)55% &
{66 
var88 
exp88 
=88 
EqlCompiler88 %
.88% &
Compile88& -
(88- .
query88. 3
,883 4
schemaProvider885 C
,88C D
methodProvider88E S
,88S T
	variables88U ^
)88^ _
;88_ `
var99 
node99 
=99 
new99 
GraphQLNode99 *
(99* +
schemaProvider99+ 9
,999 :
	fragments99; D
,99D E
name99F J
,99J K
exp99L O
,99O P
null99Q U
)99U V
;99V W
return:: 
node:: 
;:: 
};; 
else<< 
{== 
var>> 
result>> 
=>> 
EqlCompiler>> (
.>>( )
CompileWith>>) 4
(>>4 5
query>>5 :
,>>: ;
selectContext>>< I
,>>I J
schemaProvider>>K Y
,>>Y Z
methodProvider>>[ i
,>>i j
	variables>>k t
)>>t u
;>>u v
var?? 
node?? 
=?? 
new?? 
GraphQLNode?? *
(??* +
schemaProvider??+ 9
,??9 :
	fragments??; D
,??D E
name??F J
,??J K
result??L R
,??R S
null??T X
)??X Y
;??Y Z
return@@ 
node@@ 
;@@ 
}AA 
}BB 	
publicII 
overrideII 
IGraphQLBaseNodeII (
VisitEntityQueryII) 9
(II9 :
EntityGraphQLParserII: M
.IIM N
EntityQueryContextIIN `
contextIIa h
)IIh i
{JJ 	
stringKK 
nameKK 
;KK 
stringLL 
queryLL 
;LL 
ifMM 
(MM 
contextMM 
.MM 
aliasMM 
!=MM  
nullMM! %
)MM% &
{NN 
nameOO 
=OO 
contextOO 
.OO 
aliasOO $
.OO$ %
nameOO% )
.OO) *
GetTextOO* 1
(OO1 2
)OO2 3
;OO3 4
queryPP 
=PP 
contextPP 
.PP  
entityPP  &
.PP& '
GetTextPP' .
(PP. /
)PP/ 0
;PP0 1
}QQ 
elseRR 
{SS 
queryTT 
=TT 
contextTT 
.TT  
entityTT  &
.TT& '
GetTextTT' .
(TT. /
)TT/ 0
;TT0 1
nameUU 
=UU 
queryUU 
;UU 
ifVV 
(VV 
nameVV 
.VV 
IndexOfVV  
(VV  !
$strVV! $
)VV$ %
>VV& '
-VV( )
$numVV) *
)VV* +
nameWW 
=WW 
nameWW 
.WW  
	SubstringWW  )
(WW) *
$numWW* +
,WW+ ,
nameWW- 1
.WW1 2
IndexOfWW2 9
(WW9 :
$strWW: =
)WW= >
)WW> ?
;WW? @
ifXX 
(XX 
nameXX 
.XX 
IndexOfXX  
(XX  !
$strXX! $
)XX$ %
>XX& '
-XX( )
$numXX) *
)XX* +
nameYY 
=YY 
nameYY 
.YY  
	SubstringYY  )
(YY) *
$numYY* +
,YY+ ,
nameYY- 1
.YY1 2
IndexOfYY2 9
(YY9 :
$strYY: =
)YY= >
)YY> ?
;YY? @
}ZZ 
try\\ 
{]] 
CompiledQueryResult^^ #
result^^$ *
=^^+ ,
null^^- 1
;^^1 2
if__ 
(__ 
selectContext__ !
==__" $
null__% )
)__) *
{`` 
resultbb 
=bb 
EqlCompilerbb (
.bb( )
Compilebb) 0
(bb0 1
querybb1 6
,bb6 7
schemaProviderbb8 F
,bbF G
methodProviderbbH V
,bbV W
	variablesbbX a
)bba b
;bbb c
}cc 
elsedd 
{ee 
resultff 
=ff 
EqlCompilerff (
.ff( )
CompileWithff) 4
(ff4 5
queryff5 :
,ff: ;
selectContextff< I
,ffI J
schemaProviderffK Y
,ffY Z
methodProviderff[ i
,ffi j
	variablesffk t
)fft u
;ffu v
}gg 
varhh 
exphh 
=hh 
resulthh  
.hh  !
ExpressionResulthh! 1
;hh1 2
IGraphQLNodejj 
graphQLNodejj (
=jj) *
nulljj+ /
;jj/ 0
ifkk 
(kk 
expkk 
.kk 
Typekk 
.kk 
IsEnumerableOrArraykk 0
(kk0 1
)kk1 2
)kk2 3
{ll 
graphQLNodemm 
=mm  !*
BuildDynamicSelectOnCollectionmm" @
(mm@ A
resultmmA G
,mmG H
namemmI M
,mmM N
contextmmO V
,mmV W
truemmX \
)mm\ ]
;mm] ^
}nn 
elseoo 
{pp 
vartt 
listExptt 
=tt  !
Compilertt" *
.tt* +
Utiltt+ /
.tt/ 0
ExpressionUtiltt0 >
.tt> ?
FindIEnumerablett? N
(ttN O
resultttO U
.ttU V
ExpressionResultttV f
)ttf g
;ttg h
ifuu 
(uu 
listExpuu 
.uu  
Item1uu  %
!=uu& (
nulluu) -
)uu- .
{vv 
varyy 
item1yy !
=yy" #
(yy$ %
ExpressionResultyy% 5
)yy5 6
listExpyy6 =
.yy= >
Item1yy> C
;yyC D
item1zz 
.zz !
AddConstantParameterszz 3
(zz3 4
resultzz4 :
.zz: ;
ExpressionResultzz; K
.zzK L
ConstantParameterszzL ^
)zz^ _
;zz_ `
graphQLNode{{ #
={{$ %*
BuildDynamicSelectOnCollection{{& D
({{D E
new{{E H
CompiledQueryResult{{I \
({{\ ]
item1{{] b
,{{b c
result{{d j
.{{j k
ContextParams{{k x
){{x y
,{{y z
name{{{ 
,	{{ �
context
{{� �
,
{{� �
true
{{� �
)
{{� �
;
{{� �
graphQLNode|| #
.||# $
NodeExpression||$ 2
=||3 4
(||5 6
ExpressionResult||6 F
)||F G
Compiler||G O
.||O P
Util||P T
.||T U
ExpressionUtil||U c
.||c d
CombineExpressions||d v
(||v w
graphQLNode	||w �
.
||� �
NodeExpression
||� �
,
||� �
listExp
||� �
.
||� �
Item2
||� �
)
||� �
;
||� �
}}} 
else~~ 
{ 
graphQLNode
�� #
=
��$ %.
 BuildDynamicSelectForObjectGraph
��& F
(
��F G
query
��G L
,
��L M
name
��N R
,
��R S
context
��T [
,
��[ \
result
��] c
)
��c d
;
��d e
}
�� 
}
�� 
if
�� 
(
�� 
result
�� 
.
�� 

IsMutation
�� %
)
��% &
{
�� 
return
�� 
new
�� !
GraphQLMutationNode
�� 2
(
��2 3
result
��3 9
,
��9 :
graphQLNode
��; F
)
��F G
;
��G H
}
�� 
return
�� 
graphQLNode
�� "
;
��" #
}
�� 
catch
�� 
(
�� ,
EntityGraphQLCompilerException
�� 1
ex
��2 4
)
��4 5
{
�� 
throw
�� 
SchemaException
�� %
.
��% &#
MakeFieldCompileError
��& ;
(
��; <
query
��< A
,
��A B
ex
��C E
.
��E F
Message
��F M
)
��M N
;
��N O
}
�� 
}
�� 	
private
�� 
IGraphQLNode
�� ,
BuildDynamicSelectOnCollection
�� ;
(
��; <!
CompiledQueryResult
��< O
queryResult
��P [
,
��[ \
string
��] c
name
��d h
,
��h i!
EntityGraphQLParser
��j }
.
��} ~!
EntityQueryContext��~ �
context��� �
,��� �
bool��� �
isRootSelect��� �
)��� �
{
�� 	
var
�� 
elementType
�� 
=
�� 
queryResult
�� )
.
��) *
BodyType
��* 2
.
��2 3&
GetEnumerableOrArrayType
��3 K
(
��K L
)
��L M
;
��M N
var
�� 
contextParameter
��  
=
��! "

Expression
��# -
.
��- .
	Parameter
��. 7
(
��7 8
elementType
��8 C
,
��C D
$"
��E G
param_
��G M
{
��M N
elementType
��N Y
}
��Y Z
"
��Z [
)
��[ \
;
��\ ]
var
�� 
exp
�� 
=
�� 
queryResult
�� !
.
��! "
ExpressionResult
��" 2
;
��2 3
var
�� 

oldContext
�� 
=
�� 
selectContext
�� *
;
��* +
selectContext
�� 
=
�� 
contextParameter
�� ,
;
��, -
var
�� 
fieldExpressions
��  
=
��! "
context
��# *
.
��* +
fields
��+ 1
.
��1 2
children
��2 :
.
��: ;
Select
��; A
(
��A B
c
��B C
=>
��D F
Visit
��G L
(
��L M
c
��M N
)
��N O
)
��O P
.
��P Q
Where
��Q V
(
��V W
n
��W X
=>
��Y [
n
��\ ]
!=
��^ `
null
��a e
)
��e f
.
��f g
ToList
��g m
(
��m n
)
��n o
;
��o p
var
�� 
gqlNode
�� 
=
�� 
new
�� 
GraphQLNode
�� )
(
��) *
schemaProvider
��* 8
,
��8 9
	fragments
��: C
,
��C D
name
��E I
,
��I J
null
��K O
,
��O P
exp
��Q T
,
��T U
queryResult
��V a
.
��a b
ContextParams
��b o
,
��o p
fieldExpressions��q �
,��� � 
contextParameter��� �
)��� �
;��� �
selectContext
�� 
=
�� 

oldContext
�� &
;
��& '
return
�� 
gqlNode
�� 
;
�� 
}
�� 	
private
�� 
IGraphQLNode
�� .
 BuildDynamicSelectForObjectGraph
�� =
(
��= >
string
��> D
query
��E J
,
��J K
string
��L R
name
��S W
,
��W X!
EntityGraphQLParser
��Y l
.
��l m 
EntityQueryContext
��m 
context��� �
,��� �#
CompiledQueryResult��� �
	rootField��� �
)��� �
{
�� 	
var
�� 
selectWasNull
�� 
=
�� 
false
��  %
;
��% &
if
�� 
(
�� 
selectContext
�� 
==
��  
null
��! %
)
��% &
{
�� 
selectContext
�� 
=
�� 

Expression
��  *
.
��* +
	Parameter
��+ 4
(
��4 5
schemaProvider
��5 C
.
��C D
ContextType
��D O
)
��O P
;
��P Q
selectWasNull
�� 
=
�� 
true
��  $
;
��$ %
}
�� 
if
�� 
(
�� 
schemaProvider
�� 
.
�� 
TypeHasField
�� +
(
��+ ,
selectContext
��, 9
.
��9 :
Type
��: >
.
��> ?
Name
��? C
,
��C D
name
��E I
,
��I J
new
��K N
string
��O U
[
��U V
$num
��V W
]
��W X
)
��X Y
)
��Y Z
{
�� 
name
�� 
=
�� 
schemaProvider
�� %
.
��% & 
GetActualFieldName
��& 8
(
��8 9
selectContext
��9 F
.
��F G
Type
��G K
.
��K L
Name
��L P
,
��P Q
name
��R V
)
��V W
;
��W X
}
�� 
try
�� 
{
�� 
var
�� 
exp
�� 
=
�� 
(
�� 

Expression
�� %
)
��% &
	rootField
��& /
.
��/ 0
ExpressionResult
��0 @
;
��@ A
var
�� 

oldContext
�� 
=
��  
selectContext
��! .
;
��. /
var
�� 
rootFieldParam
�� "
=
��# $

Expression
��% /
.
��/ 0
	Parameter
��0 9
(
��9 :
exp
��: =
.
��= >
Type
��> B
)
��B C
;
��C D
selectContext
�� 
=
�� 
	rootField
��  )
.
��) *

IsMutation
��* 4
?
��5 6
rootFieldParam
��7 E
:
��F G
exp
��H K
;
��K L
var
�� 
fieldExpressions
�� $
=
��% &
context
��' .
.
��. /
fields
��/ 5
.
��5 6
children
��6 >
.
��> ?
Select
��? E
(
��E F
c
��F G
=>
��H J
Visit
��K P
(
��P Q
c
��Q R
)
��R S
)
��S T
.
��T U
Where
��U Z
(
��Z [
n
��[ \
=>
��] _
n
��` a
!=
��b d
null
��e i
)
��i j
.
��j k
ToList
��k q
(
��q r
)
��r s
;
��s t
var
�� 
graphQLNode
�� 
=
��  !
new
��" %
GraphQLNode
��& 1
(
��1 2
schemaProvider
��2 @
,
��@ A
	fragments
��B K
,
��K L
name
��M Q
,
��Q R
null
��S W
,
��W X
(
��Y Z
ExpressionResult
��Z j
)
��j k
selectContext
��k x
,
��x y
(
��z {
	rootField��{ �
.��� �

IsMutation��� �
?��� �
new��� �#
ParameterExpression��� �
[��� �
]��� �
{��� �
rootFieldParam��� �
}��� �
:��� �
	rootField��� �
.��� �
ContextParams��� �
.��� �
ToArray��� �
(��� �
)��� �
)��� �
,��� � 
fieldExpressions��� �
,��� �
null��� �
)��� �
;��� �
if
�� 
(
�� 
	rootField
�� 
!=
��  
null
��! %
&&
��& (
	rootField
��) 2
.
��2 3 
ConstantParameters
��3 E
!=
��F H
null
��I M
)
��M N
{
�� 
graphQLNode
�� 
.
��  #
AddConstantParameters
��  5
(
��5 6
	rootField
��6 ?
.
��? @ 
ConstantParameters
��@ R
)
��R S
;
��S T
}
�� 
selectContext
�� 
=
�� 

oldContext
��  *
;
��* +
if
�� 
(
�� 
selectWasNull
�� !
)
��! "
{
�� 
selectContext
�� !
=
��" #
null
��$ (
;
��( )
}
�� 
return
�� 
graphQLNode
�� "
;
��" #
}
�� 
catch
�� 
(
�� ,
EntityGraphQLCompilerException
�� 1
ex
��2 4
)
��4 5
{
�� 
throw
�� 
SchemaException
�� %
.
��% &#
MakeFieldCompileError
��& ;
(
��; <
query
��< A
,
��A B
ex
��C E
.
��E F
Message
��F M
)
��M N
;
��N O
}
�� 
}
�� 	
public
�� 
override
�� 
IGraphQLBaseNode
�� (
VisitGraphQL
��) 5
(
��5 6!
EntityGraphQLParser
��6 I
.
��I J
GraphQLContext
��J X
context
��Y `
)
��` a
{
�� 	
foreach
�� 
(
�� 
var
�� 
c
�� 
in
�� 
context
�� %
.
��% &
children
��& .
)
��. /
{
�� 
Visit
�� 
(
�� 
c
�� 
)
�� 
;
�� 
}
�� 
return
�� 
new
�� 
GraphQLResultNode
�� (
(
��( )
rootQueries
��) 4
,
��4 5
	fragments
��6 ?
)
��? @
;
��@ A
}
�� 	
public
�� 
override
�� 
IGraphQLBaseNode
�� (
VisitDataQuery
��) 7
(
��7 8!
EntityGraphQLParser
��8 K
.
��K L
DataQueryContext
��L \
context
��] d
)
��d e
{
�� 	
var
�� 
	operation
�� 
=
�� 
GetOperation
�� (
(
��( )
context
��) 0
.
��0 1
operationName
��1 >
(
��> ?
)
��? @
)
��@ A
;
��A B
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��  
	operation
��! *
.
��* +
	Arguments
��+ 4
.
��4 5
Where
��5 :
(
��: ;
a
��; <
=>
��= ?
a
��@ A
.
��A B
DefaultValue
��B N
!=
��O Q
null
��R V
)
��V W
)
��W X
{
�� 
	variables
�� 
[
�� 
item
�� 
.
�� 
ArgName
�� &
]
��& '
=
��( )

Expression
��* 4
.
��4 5
Lambda
��5 ;
(
��; <
item
��< @
.
��@ A
DefaultValue
��A M
.
��M N

Expression
��N X
)
��X Y
.
��Y Z
Compile
��Z a
(
��a b
)
��b c
.
��c d
DynamicInvoke
��d q
(
��q r
)
��r s
;
��s t
}
�� 
var
�� 
query
�� 
=
�� 
new
�� 
GraphQLNode
�� '
(
��' (
schemaProvider
��( 6
,
��6 7
	fragments
��8 A
,
��A B
	operation
��C L
.
��L M
Name
��M Q
,
��Q R
null
��S W
,
��W X
null
��Y ]
,
��] ^
null
��_ c
,
��c d
null
��e i
,
��i j
null
��k o
)
��o p
;
��p q
foreach
�� 
(
�� 
var
�� 
c
�� 
in
�� 
context
�� %
.
��% &
gqlBody
��& -
(
��- .
)
��. /
.
��/ 0
children
��0 8
)
��8 9
{
�� 
var
�� 
n
�� 
=
�� 
Visit
�� 
(
�� 
c
�� 
)
��  
;
��  !
if
�� 
(
�� 
n
�� 
!=
�� 
null
�� 
)
�� 
query
�� 
.
�� 
AddField
�� "
(
��" #
(
��# $
IGraphQLNode
��$ 0
)
��0 1
n
��1 2
)
��2 3
;
��3 4
}
�� 
rootQueries
�� 
.
�� 
Add
�� 
(
�� 
query
�� !
)
��! "
;
��" #
return
�� 
query
�� 
;
�� 
}
�� 	
public
�� 
override
�� 
IGraphQLBaseNode
�� ( 
VisitMutationQuery
��) ;
(
��; <!
EntityGraphQLParser
��< O
.
��O P"
MutationQueryContext
��P d
context
��e l
)
��l m
{
�� 	
var
�� 
	operation
�� 
=
�� 
GetOperation
�� (
(
��( )
context
��) 0
.
��0 1
operationName
��1 >
(
��> ?
)
��? @
)
��@ A
;
��A B
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��  
	operation
��! *
.
��* +
	Arguments
��+ 4
.
��4 5
Where
��5 :
(
��: ;
a
��; <
=>
��= ?
a
��@ A
.
��A B
DefaultValue
��B N
!=
��O Q
null
��R V
)
��V W
)
��W X
{
�� 
	variables
�� 
[
�� 
item
�� 
.
�� 
ArgName
�� &
]
��& '
=
��( )

Expression
��* 4
.
��4 5
Lambda
��5 ;
(
��; <
item
��< @
.
��@ A
DefaultValue
��A M
.
��M N

Expression
��N X
)
��X Y
.
��Y Z
Compile
��Z a
(
��a b
)
��b c
.
��c d
DynamicInvoke
��d q
(
��q r
)
��r s
;
��s t
}
�� 
var
�� 
mutation
�� 
=
�� 
new
�� 
GraphQLNode
�� *
(
��* +
schemaProvider
��+ 9
,
��9 :
	fragments
��; D
,
��D E
	operation
��F O
.
��O P
Name
��P T
,
��T U
null
��V Z
,
��Z [
null
��\ `
,
��` a
null
��b f
,
��f g
null
��h l
,
��l m
null
��n r
)
��r s
;
��s t
foreach
�� 
(
�� 
var
�� 
c
�� 
in
�� 
context
�� %
.
��% &
gqlBody
��& -
(
��- .
)
��. /
.
��/ 0
children
��0 8
)
��8 9
{
�� 
var
�� 
n
�� 
=
�� 
Visit
�� 
(
�� 
c
�� 
)
��  
;
��  !
if
�� 
(
�� 
n
�� 
!=
�� 
null
�� 
)
�� 
{
�� 
mutation
�� 
.
�� 
AddField
�� %
(
��% &
(
��& '
IGraphQLNode
��' 3
)
��3 4
n
��4 5
)
��5 6
;
��6 7
}
�� 
}
�� 
rootQueries
�� 
.
�� 
Add
�� 
(
�� 
mutation
�� $
)
��$ %
;
��% &
return
�� 
mutation
�� 
;
�� 
}
�� 	
public
�� 
GraphQLOperation
�� 
GetOperation
��  ,
(
��, -!
EntityGraphQLParser
��- @
.
��@ A"
OperationNameContext
��A U
context
��V ]
)
��] ^
{
�� 	
if
�� 
(
�� 
context
�� 
==
�� 
null
�� 
)
��  
{
�� 
return
�� 
new
�� 
GraphQLOperation
�� +
(
��+ ,
)
��, -
;
��- .
}
�� 
var
�� 
visitor
�� 
=
�� 
new
�� 
OperationVisitor
�� .
(
��. /
	variables
��/ 8
,
��8 9
schemaProvider
��: H
)
��H I
;
��I J
var
�� 
op
�� 
=
�� 
visitor
�� 
.
�� 
Visit
�� "
(
��" #
context
��# *
)
��* +
;
��+ ,
return
�� 
op
�� 
;
�� 
}
�� 	
public
�� 
override
�� 
IGraphQLBaseNode
�� (
VisitGqlFragment
��) 9
(
��9 :!
EntityGraphQLParser
��: M
.
��M N 
GqlFragmentContext
��N `
context
��a h
)
��h i
{
�� 	
var
�� 
typeName
�� 
=
�� 
context
�� "
.
��" #
fragmentType
��# /
.
��/ 0
GetText
��0 7
(
��7 8
)
��8 9
;
��9 :
selectContext
�� 
=
�� 

Expression
�� &
.
��& '
	Parameter
��' 0
(
��0 1
schemaProvider
��1 ?
.
��? @
Type
��@ D
(
��D E
typeName
��E M
)
��M N
.
��N O
ContextType
��O Z
,
��Z [
$"
��\ ^
	fragment_
��^ g
{
��g h
typeName
��h p
}
��p q
"
��q r
)
��r s
;
��s t
var
�� 
fields
�� 
=
�� 
new
�� 
List
�� !
<
��! "
IGraphQLBaseNode
��" 2
>
��2 3
(
��3 4
)
��4 5
;
��5 6
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��  
context
��! (
.
��( )
fields
��) /
.
��/ 0
children
��0 8
)
��8 9
{
�� 
var
�� 
f
�� 
=
�� 
Visit
�� 
(
�� 
item
�� "
)
��" #
;
��# $
if
�� 
(
�� 
f
�� 
!=
�� 
null
�� 
)
�� 
fields
�� 
.
�� 
Add
�� 
(
�� 
f
��  
)
��  !
;
��! "
}
�� 
	fragments
�� 
.
�� 
Add
�� 
(
�� 
new
�� 
GraphQLFragment
�� -
(
��- .
context
��. 5
.
��5 6
fragmentName
��6 B
.
��B C
GetText
��C J
(
��J K
)
��K L
,
��L M
typeName
��N V
,
��V W
fields
��X ^
,
��^ _
(
��` a!
ParameterExpression
��a t
)
��t u
selectContext��u �
)��� �
)��� �
;��� �
selectContext
�� 
=
�� 
null
��  
;
��  !
return
�� 
null
�� 
;
�� 
}
�� 	
public
�� 
override
�� 
IGraphQLBaseNode
�� (!
VisitFragmentSelect
��) <
(
��< =!
EntityGraphQLParser
��= P
.
��P Q#
FragmentSelectContext
��Q f
context
��g n
)
��n o
{
�� 	
var
�� 
name
�� 
=
�� 
context
�� 
.
�� 
name
�� #
.
��# $
GetText
��$ +
(
��+ ,
)
��, -
;
��- .
return
�� 
new
�� #
GraphQLFragmentSelect
�� ,
(
��, -
name
��- 1
)
��1 2
;
��2 3
}
�� 	
}
�� 
}�� �
CY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\IGraphQLNode.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
public		 

	interface		 
IGraphQLNode		 !
:		" #
IGraphQLBaseNode		$ 4
{

 
object 
Execute 
( 
params 
object $
[$ %
]% &
args' +
)+ ,
;, -
IEnumerable 
< 
IGraphQLNode  
>  !
Fields" (
{) *
get+ .
;. /
}0 1
ExpressionResult 
NodeExpression '
{( )
get* -
;- .
set/ 2
;2 3
}4 5
List## 
<## 
ParameterExpression##  
>##  !

Parameters##" ,
{##- .
get##/ 2
;##2 3
}##4 5
IReadOnlyDictionary(( 
<(( 
ParameterExpression(( /
,((/ 0
object((1 7
>((7 8
ConstantParameters((9 K
{((L M
get((N Q
;((Q R
}((S T
})) 
public++ 

	interface++ 
IGraphQLBaseNode++ %
{,, 
string11 
Name11 
{11 
get11 
;11 
}11 
OperationType22 
Type22 
{22 
get22  
;22  !
}22" #
}33 
public55 

enum55 
OperationType55 
{66 
Query77 
,77 
Mutation88 
,88 
Fragment99 
,99 
Result:: 
,:: 
};; 
}<< �
EY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\MutationResult.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
public		 

class		 
MutationResult		 
:		  !
ExpressionResult		" 2
{

 
private 
string 
method 
; 
private 
readonly 
Schema 
.  
MutationType  ,
mutationType- 9
;9 :
private 
readonly 

Expression #
paramExp$ ,
;, -
private 

Dictionary 
< 
string !
,! "
ExpressionResult# 3
>3 4
gqlRequestArgs5 C
;C D
public 
MutationResult 
( 
string $
method% +
,+ ,
Schema- 3
.3 4
MutationType4 @
mutationTypeA M
,M N

DictionaryO Y
<Y Z
stringZ `
,` a
ExpressionResultb r
>r s
argst x
)x y
:z {
base	| �
(
� �
null
� �
)
� �
{ 	
this 
. 
method 
= 
method  
;  !
this 
. 
mutationType 
= 
mutationType  ,
;, -
this 
. 
gqlRequestArgs 
=  !
args" &
;& '
paramExp 
= 

Expression !
.! "
	Parameter" +
(+ ,
mutationType, 8
.8 9
ContextType9 D
)D E
;E F
} 	
public 
override 

Expression "

Expression# -
{. /
get0 3
{4 5
return6 <
paramExp= E
;E F
}G H
}I J
public 
object 
Execute 
( 
object $
[$ %
]% &
externalArgs' 3
)3 4
{ 	
try 
{ 
return 
mutationType #
.# $
Call$ (
(( )
externalArgs) 5
,5 6
gqlRequestArgs7 E
)E F
;F G
} 
catch   
(   "
EntityQuerySchemaError   (
e  ) *
)  * +
{!! 
throw"" 
new"" "
EntityQuerySchemaError"" 0
(""0 1
$"""1 3%
Error applying mutation: ""3 L
{""L M
e""M N
.""N O
Message""O V
}""V W
"""W X
)""X Y
;""Y Z
}## 
}$$ 	
}%% 
}&& �D
GY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\OperationVisitor.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
internal 
class 
OperationVisitor #
:$ %$
EntityGraphQLBaseVisitor& >
<> ?
GraphQLOperation? O
>O P
{ 
private 
QueryVariables 
	variables (
;( )
private		 
readonly		 
Schema		 
.		  
ISchemaProvider		  /
schemaProvider		0 >
;		> ?
private

 
GraphQLOperation

  
	operation

! *
;

* +
public 
OperationVisitor 
(  
QueryVariables  .
	variables/ 8
,8 9
Schema: @
.@ A
ISchemaProviderA P
schemaProviderQ _
)_ `
{ 	
this 
. 
	variables 
= 
	variables &
;& '
this 
. 
schemaProvider 
=  !
schemaProvider" 0
;0 1
this 
. 
	operation 
= 
new  
GraphQLOperation! 1
(1 2
)2 3
;3 4
} 	
public 
override 
GraphQLOperation (
VisitOperationName) ;
(; <
EntityGraphQLParser< O
.O P 
OperationNameContextP d
contexte l
)l m
{ 	
this 
. 
	operation 
. 
Name 
=  !
context" )
.) *
	operation* 3
.3 4
GetText4 ;
(; <
)< =
;= >
if 
( 
context 
. 
operationArgs %
!=& (
null) -
)- .
{ 
Visit 
( 
context 
. 
operationArgs +
)+ ,
;, -
} 
return 
this 
. 
	operation !
;! "
} 	
public 
override 
GraphQLOperation (
VisitGqlTypeDef) 8
(8 9
EntityGraphQLParser9 L
.L M
GqlTypeDefContextM ^
context_ f
)f g
{ 	
var 
argName 
= 
context !
.! "
gqlVar" (
(( )
)) *
.* +
GetText+ 2
(2 3
)3 4
.4 5
	TrimStart5 >
(> ?
$char? B
)B C
;C D
var   
isArray   
=   
context   !
.  ! "
	arrayType  " +
!=  , .
null  / 3
;  3 4
var!! 
type!! 
=!! 
isArray!! 
?!!  
context!!! (
.!!( )
	arrayType!!) 2
.!!2 3
type!!3 7
.!!7 8
GetText!!8 ?
(!!? @
)!!@ A
:!!B C
context!!D K
.!!K L
type!!L P
.!!P Q
GetText!!Q X
(!!X Y
)!!Y Z
;!!Z [
var"" 
required"" 
="" 
context"" "
.""" #
required""# +
!="", .
null""/ 3
;""3 4
CompiledQueryResult## 
defaultValue##  ,
=##- .
null##/ 3
;##3 4
if$$ 
($$ 
context$$ 
.$$ 
defaultValue$$ $
!=$$% '
null$$( ,
)$$, -
{%% 
defaultValue&& 
=&& 
EqlCompiler&& *
.&&* +
CompileWith&&+ 6
(&&6 7
context&&7 >
.&&> ?
defaultValue&&? K
.&&K L
GetText&&L S
(&&S T
)&&T U
,&&U V
null&&W [
,&&[ \
schemaProvider&&] k
,&&k l
null&&m q
,&&q r
	variables&&s |
)&&| }
;&&} ~
}'' 
if)) 
()) 
required)) 
&&)) 
!)) 
	variables)) &
.))& '
ContainsKey))' 2
())2 3
argName))3 :
))): ;
&&))< >
defaultValue))? K
==))L N
null))O S
)))S T
{** 
throw++ 
new++ 
QueryException++ (
(++( )
$"++) +'
Missing required variable '+++ F
{++F G
argName++G N
}++N O
' on query '++O [
{++[ \
this++\ `
.++` a
	operation++a j
.++j k
Name++k o
}++o p
'++p q
"++q r
)++r s
;++s t
},, 
this.. 
... 
	operation.. 
... 
AddArgument.. &
(..& '
argName..' .
,... /
type..0 4
,..4 5
isArray..6 =
,..= >
required..? G
,..G H
defaultValue..I U
!=..V X
null..Y ]
?..^ _
defaultValue..` l
...l m
ExpressionResult..m }
:..~ 
null
..� �
)
..� �
;
..� �
return00 
this00 
.00 
	operation00 !
;00! "
}11 	
}22 
internal44 
class44 
GraphQLOperation44 #
{55 
public66 
IEnumerable66 
<66 $
GraphQlOperationArgument66 3
>663 4
	Arguments665 >
=>66? A
	arguments66B K
;66K L
private77 
List77 
<77 $
GraphQlOperationArgument77 -
>77- .
	arguments77/ 8
;778 9
public99 
GraphQLOperation99 
(99  
)99  !
{:: 	
	arguments;; 
=;; 
new;; 
List;;  
<;;  !$
GraphQlOperationArgument;;! 9
>;;9 :
(;;: ;
);;; <
;;;< =
}<< 	
public>> 
string>> 
Name>> 
{>> 
get>>  
;>>  !
internal>>" *
set>>+ .
;>>. /
}>>0 1
internal@@ 
void@@ 
AddArgument@@ !
(@@! "
string@@" (
argName@@) 0
,@@0 1
object@@2 8
type@@9 =
,@@= >
bool@@? C
isArray@@D K
,@@K L
bool@@M Q
required@@R Z
,@@Z [
ExpressionResult@@\ l
defaultValue@@m y
)@@y z
{AA 	
	argumentsBB 
.BB 
AddBB 
(BB 
newBB $
GraphQlOperationArgumentBB 6
(BB6 7
argNameBB7 >
,BB> ?
typeBB@ D
,BBD E
isArrayBBF M
,BBM N
requiredBBO W
,BBW X
defaultValueBBY e
)BBe f
)BBf g
;BBg h
}CC 	
}DD 
internalFF 
classFF $
GraphQlOperationArgumentFF +
{GG 
publicHH $
GraphQlOperationArgumentHH '
(HH' (
stringHH( .
argNameHH/ 6
,HH6 7
objectHH8 >
typeHH? C
,HHC D
boolHHE I
isArrayHHJ Q
,HHQ R
boolHHS W
requiredHHX `
,HH` a
ExpressionResultHHb r
defaultValueHHs 
)	HH �
{II 	
thisJJ 
.JJ 
ArgNameJJ 
=JJ 
argNameJJ "
;JJ" #
thisKK 
.KK 
TypeKK 
=KK 
typeKK 
;KK 
thisLL 
.LL 
IsArrayLL 
=LL 
isArrayLL "
;LL" #
thisMM 
.MM 
RequiredMM 
=MM 
requiredMM $
;MM$ %
DefaultValueNN 
=NN 
defaultValueNN '
;NN' (
}OO 	
publicQQ 
stringQQ 
ArgNameQQ 
{QQ 
getQQ  #
;QQ# $
}QQ% &
publicRR 
objectRR 
TypeRR 
{RR 
getRR  
;RR  !
}RR" #
publicSS 
boolSS 
IsArraySS 
{SS 
getSS !
;SS! "
}SS# $
publicTT 
boolTT 
RequiredTT 
{TT 
getTT "
;TT" #
}TT$ %
publicUU 
ExpressionResultUU 
DefaultValueUU  ,
{UU- .
getUU/ 2
;UU2 3
}UU4 5
}VV 
}WW ��
NY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\QueryGrammerNodeVisitor.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
{ 
internal 
class #
QueryGrammerNodeVisitor *
:+ ,$
EntityGraphQLBaseVisitor- E
<E F
ExpressionResultF V
>V W
{ 
private 
ExpressionResult  
currentContext! /
;/ 0
private 
ISchemaProvider 
schemaProvider  .
;. /
private 
IMethodProvider 
methodProvider  .
;. /
private 
readonly 
QueryVariables '
	variables( 1
;1 2
private 
IMethodType  
fieldArgumentContext 0
;0 1
private 
Regex 
	guidRegex 
=  !
new" %
Regex& +
(+ ,
$str, `
,` a
RegexOptionsb n
.n o

IgnoreCaseo y
)y z
;z {
public #
QueryGrammerNodeVisitor &
(& '

Expression' 1

expression2 <
,< =
ISchemaProvider> M
schemaProviderN \
,\ ]
IMethodProvider^ m
methodProvidern |
,| }
QueryVariables	~ �
	variables
� �
)
� �
{ 	
currentContext 
= 
( 
ExpressionResult .
). /

expression/ 9
;9 :
this 
. 
schemaProvider 
=  !
schemaProvider" 0
;0 1
this 
. 
methodProvider 
=  !
methodProvider" 0
;0 1
this 
. 
	variables 
= 
	variables &
;& '
} 	
public   
override   
ExpressionResult   (
VisitBinary  ) 4
(  4 5
EntityGraphQLParser  5 H
.  H I
BinaryContext  I V
context  W ^
)  ^ _
{!! 	
var"" 
left"" 
="" 
Visit"" 
("" 
context"" $
.""$ %
left""% )
)"") *
;""* +
var## 
right## 
=## 
Visit## 
(## 
context## %
.##% &
right##& +
)##+ ,
;##, -
var$$ 
op$$ 
=$$ 
MakeOperator$$ !
($$! "
context$$" )
.$$) *
op$$* ,
.$$, -
GetText$$- 4
($$4 5
)$$5 6
)$$6 7
;$$7 8
if&& 
(&& 
left&& 
.&& 
Type&& 
!=&& 
right&& "
.&&" #
Type&&# '
)&&' (
{'' 
if(( 
((( 
op(( 
==(( 
ExpressionType(( (
.((( )
Equal(() .
||((/ 1
op((2 4
==((5 7
ExpressionType((8 F
.((F G
NotEqual((G O
)((O P
{)) 
var** 
result** 
=**  .
"DoObjectComparisonOnDifferentTypes**! C
(**C D
op**D F
,**F G
left**H L
,**L M
right**N S
)**S T
;**T U
if,, 
(,, 
result,, 
!=,, !
null,," &
),,& '
return-- 
result-- %
;--% &
}.. 
return// 
ConvertLeftOrRight// )
(//) *
op//* ,
,//, -
left//. 2
,//2 3
right//4 9
)//9 :
;//: ;
}00 
if22 
(22 
op22 
==22 
ExpressionType22 $
.22$ %
Add22% (
&&22) +
left22, 0
.220 1
Type221 5
==226 8
typeof229 ?
(22? @
string22@ F
)22F G
&&22H J
right22K P
.22P Q
Type22Q U
==22V X
typeof22Y _
(22_ `
string22` f
)22f g
)22g h
{33 
return44 
(44 
ExpressionResult44 (
)44( )

Expression44) 3
.443 4
Call444 8
(448 9
null449 =
,44= >
typeof44? E
(44E F
string44F L
)44L M
.44M N
	GetMethod44N W
(44W X
$str44X `
,44` a
new44b e
[44e f
]44f g
{44h i
typeof44j p
(44p q
string44q w
)44w x
,44x y
typeof	44z �
(
44� �
string
44� �
)
44� �
}
44� �
)
44� �
,
44� �
left
44� �
,
44� �
right
44� �
)
44� �
;
44� �
}55 
return77 
(77 
ExpressionResult77 $
)77$ %

Expression77% /
.77/ 0

MakeBinary770 :
(77: ;
op77; =
,77= >
left77? C
,77C D
right77E J
)77J K
;77K L
}88 	
private:: 
ExpressionResult::  .
"DoObjectComparisonOnDifferentTypes::! C
(::C D
ExpressionType::D R
op::S U
,::U V
ExpressionResult::W g
left::h l
,::l m
ExpressionResult::n ~
right	:: �
)
::� �
{;; 	
var<<  
convertedToSameTypes<< $
=<<% &
false<<' ,
;<<, -
if@@ 
(@@ 
left@@ 
.@@ 
Type@@ 
==@@ 
typeof@@ #
(@@# $
Guid@@$ (
)@@( )
&&@@* ,
right@@- 2
.@@2 3
Type@@3 7
!=@@8 :
typeof@@; A
(@@A B
Guid@@B F
)@@F G
)@@G H
{AA 
rightBB 
=BB 
ConvertToGuidBB %
(BB% &
rightBB& +
)BB+ ,
;BB, - 
convertedToSameTypesCC $
=CC% &
trueCC' +
;CC+ ,
}DD 
elseEE 
ifEE 
(EE 
rightEE 
.EE 
TypeEE 
==EE  "
typeofEE# )
(EE) *
GuidEE* .
)EE. /
&&EE0 2
leftEE3 7
.EE7 8
TypeEE8 <
!=EE= ?
typeofEE@ F
(EEF G
GuidEEG K
)EEK L
)EEL M
{FF 
leftGG 
=GG 
ConvertToGuidGG $
(GG$ %
leftGG% )
)GG) *
;GG* + 
convertedToSameTypesHH $
=HH% &
trueHH' +
;HH+ ,
}II 
returnKK  
convertedToSameTypesKK '
?KK( )
(KK* +
ExpressionResultKK+ ;
)KK; <

ExpressionKK< F
.KKF G

MakeBinaryKKG Q
(KKQ R
opKKR T
,KKT U
leftKKV Z
,KKZ [
rightKK\ a
)KKa b
:KKc d
nullKKe i
;KKi j
}LL 	
privateNN 
staticNN 
ExpressionResultNN '
ConvertToGuidNN( 5
(NN5 6
ExpressionResultNN6 F

expressionNNG Q
)NNQ R
{OO 	
returnPP 
(PP 
ExpressionResultPP $
)PP$ %

ExpressionPP% /
.PP/ 0
CallPP0 4
(PP4 5
typeofPP5 ;
(PP; <
GuidPP< @
)PP@ A
,PPA B
$strPPC J
,PPJ K
nullPPL P
,PPP Q
(PPR S
ExpressionResultPPS c
)PPc d

ExpressionPPd n
.PPn o
CallPPo s
(PPs t

expressionPPt ~
,PP~ 
typeof
PP� �
(
PP� �
object
PP� �
)
PP� �
.
PP� �
	GetMethod
PP� �
(
PP� �
$str
PP� �
)
PP� �
)
PP� �
)
PP� �
;
PP� �
}QQ 	
publicSS 
overrideSS 
ExpressionResultSS (
	VisitExprSS) 2
(SS2 3
EntityGraphQLParserSS3 F
.SSF G
ExprContextSSG R
contextSSS Z
)SSZ [
{TT 	
varUU 
rUU 
=UU 
VisitUU 
(UU 
contextUU !
.UU! "
bodyUU" &
)UU& '
;UU' (
returnVV 
rVV 
;VV 
}WW 	
publicYY 
overrideYY 
ExpressionResultYY (
VisitCallPathYY) 6
(YY6 7
EntityGraphQLParserYY7 J
.YYJ K
CallPathContextYYK Z
contextYY[ b
)YYb c
{ZZ 	
var[[ 
startingContext[[ 
=[[  !
currentContext[[" 0
;[[0 1
ExpressionResult\\ 
exp\\  
=\\! "
null\\# '
;\\' (
foreach]] 
(]] 
var]] 
child]] 
in]] !
context]]" )
.]]) *
children]]* 2
)]]2 3
{^^ 
var__ 
r__ 
=__ 
Visit__ 
(__ 
child__ #
)__# $
;__$ %
if`` 
(`` 
r`` 
==`` 
null`` 
)`` 
continueaa 
;aa 
ifcc 
(cc 
expcc 
!=cc 
nullcc 
)cc  
{dd 
ree 
.ee !
AddConstantParametersee +
(ee+ ,
expee, /
.ee/ 0
ConstantParametersee0 B
)eeB C
;eeC D
}ff 
expgg 
=gg 
rgg 
;gg 
currentContexthh 
=hh  
exphh! $
;hh$ %
}ii 
currentContextjj 
=jj 
startingContextjj ,
;jj, -
returnkk 
expkk 
;kk 
}ll 	
publicnn 
overridenn 
ExpressionResultnn (
VisitIdentitynn) 6
(nn6 7
EntityGraphQLParsernn7 J
.nnJ K
IdentityContextnnK Z
contextnn[ b
)nnb c
{oo 	
varpp 
fieldpp 
=pp 
contextpp 
.pp  
GetTextpp  '
(pp' (
)pp( )
;pp) *
returnqq 
MakeFieldExpressionqq &
(qq& '
fieldqq' ,
,qq, -
nullqq. 2
)qq2 3
;qq3 4
}rr 	
publictt 
overridett 
ExpressionResulttt (
VisitGqlcalltt) 5
(tt5 6
EntityGraphQLParsertt6 I
.ttI J
GqlcallContextttJ X
contextttY `
)tt` a
{uu 	
varvv 
	fieldNamevv 
=vv 
contextvv #
.vv# $
methodvv$ *
.vv* +
GetTextvv+ 2
(vv2 3
)vv3 4
;vv4 5
varww 
argListww 
=ww 
contextww !
.ww! "
gqlargumentsww" .
.ww. /
childrenww/ 7
.ww7 8
Whereww8 =
(ww= >
cww> ?
=>ww@ B
cwwC D
.wwD E
GetTypewwE L
(wwL M
)wwM N
==wwO Q
typeofwwR X
(wwX Y
EntityGraphQLParserwwY l
.wwl m
GqlargContextwwm z
)wwz {
)ww{ |
.ww| }
Cast	ww} �
<
ww� �!
EntityGraphQLParser
ww� �
.
ww� �
GqlargContext
ww� �
>
ww� �
(
ww� �
)
ww� �
;
ww� �
IMethodTypexx 

methodTypexx "
=xx# $
schemaProviderxx% 3
.xx3 4
GetFieldTypexx4 @
(xx@ A
currentContextxxA O
,xxO P
	fieldNamexxQ Z
)xxZ [
;xx[ \
varyy 
argsyy 
=yy 
argListyy 
.yy 
ToDictionaryyy +
(yy+ ,
ayy, -
=>yy. 0
ayy1 2
.yy2 3
gqlfieldyy3 ;
.yy; <
GetTextyy< C
(yyC D
)yyD E
,yyE F
ayyG H
=>yyI K
{yyL M
varzz 
argNamezz 
=zz 
azz 
.zz  
gqlfieldzz  (
.zz( )
GetTextzz) 0
(zz0 1
)zz1 2
;zz2 3
if{{ 
({{ 
!{{ 

methodType{{ 
.{{  
	Arguments{{  )
.{{) *
ContainsKey{{* 5
({{5 6
argName{{6 =
){{= >
){{> ?
{|| 
throw}} 
new}} *
EntityGraphQLCompilerException}} <
(}}< =
$"}}= ?
No argument '}}? L
{}}L M
argName}}M T
}}}T U
' found on field '}}U g
{}}g h

methodType}}h r
.}}r s
Name}}s w
}}}w x
'}}x y
"}}y z
)}}z {
;}}{ |
}~~  
fieldArgumentContext $
=% &

methodType' 1
;1 2
var
�� 
r
�� 
=
�� 
VisitGqlarg
�� #
(
��# $
a
��$ %
)
��% &
;
��& '"
fieldArgumentContext
�� $
=
��% &
null
��' +
;
��+ ,
return
�� 
r
�� 
;
�� 
}
�� 
)
�� 
;
�� 
if
�� 
(
�� 
schemaProvider
�� 
.
�� 
HasMutation
�� *
(
��* +
	fieldName
��+ 4
)
��4 5
)
��5 6
{
�� 
return
�� $
MakeMutationExpression
�� -
(
��- .
	fieldName
��. 7
,
��7 8
(
��9 :
MutationType
��: F
)
��F G

methodType
��G Q
,
��Q R
args
��S W
)
��W X
;
��X Y
}
�� 
return
�� !
MakeFieldExpression
�� &
(
��& '
	fieldName
��' 0
,
��0 1
args
��2 6
)
��6 7
;
��7 8
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
VisitGqlarg
��) 4
(
��4 5!
EntityGraphQLParser
��5 H
.
��H I
GqlargContext
��I V
context
��W ^
)
��^ _
{
�� 	
ExpressionResult
�� 
gqlVarValue
�� (
=
��) *
null
��+ /
;
��/ 0
if
�� 
(
�� 
context
�� 
.
�� 
gqlVar
�� 
(
�� 
)
��  
!=
��! #
null
��$ (
)
��( )
{
�� 
string
�� 
varKey
�� 
=
�� 
context
��  '
.
��' (
gqlVar
��( .
(
��. /
)
��/ 0
.
��0 1
GetText
��1 8
(
��8 9
)
��9 :
.
��: ;
	TrimStart
��; D
(
��D E
$char
��E H
)
��H I
;
��I J
object
�� 
value
�� 
=
�� 
	variables
�� (
.
��( )
GetValueFor
��) 4
(
��4 5
varKey
��5 ;
)
��; <
;
��< =
gqlVarValue
�� 
=
�� 
(
�� 
ExpressionResult
�� /
)
��/ 0

Expression
��0 :
.
��: ;
Constant
��; C
(
��C D
value
��D I
)
��I J
;
��J K
}
�� 
else
�� 
{
�� 
gqlVarValue
�� 
=
�� 
Visit
�� #
(
��# $
context
��$ +
.
��+ ,
gqlvalue
��, 4
)
��4 5
;
��5 6
}
�� 
string
�� 
argName
�� 
=
�� 
context
�� $
.
��$ %
gqlfield
��% -
.
��- .
GetText
��. 5
(
��5 6
)
��6 7
;
��7 8
if
�� 
(
�� "
fieldArgumentContext
�� $
.
��$ %
HasArgumentByName
��% 6
(
��6 7
argName
��7 >
)
��> ?
)
��? @
{
�� 
var
�� 
argType
�� 
=
�� "
fieldArgumentContext
�� 2
.
��2 3
GetArgumentType
��3 B
(
��B C
argName
��C J
)
��J K
;
��K L
if
�� 
(
�� 
gqlVarValue
�� 
!=
��  "
null
��# '
&&
��( *
gqlVarValue
��+ 6
.
��6 7
Type
��7 ;
==
��< >
typeof
��? E
(
��E F
string
��F L
)
��L M
&&
��N P
gqlVarValue
��Q \
.
��\ ]
NodeType
��] e
==
��f h
ExpressionType
��i w
.
��w x
Constant��x �
)��� �
{
�� 
string
�� 
strValue
�� #
=
��$ %
(
��& '
string
��' -
)
��- .
(
��. /
(
��/ 0 
ConstantExpression
��0 B
)
��B C
gqlVarValue
��C N
)
��N O
.
��O P
Value
��P U
;
��U V
if
�� 
(
�� 
	guidRegex
�� !
.
��! "
IsMatch
��" )
(
��) *
strValue
��* 2
)
��2 3
)
��3 4
{
�� 
return
�� 
ConvertToGuid
�� ,
(
��, -
gqlVarValue
��- 8
)
��8 9
;
��9 :
}
�� 
if
�� 
(
�� 
argType
�� 
.
��  &
IsConstructedGenericType
��  8
&&
��9 ;
argType
��< C
.
��C D&
GetGenericTypeDefinition
��D \
(
��\ ]
)
��] ^
==
��_ a
typeof
��b h
(
��h i
EntityQueryType
��i x
<
��x y
>
��y z
)
��z {
)
��{ |
{
�� 
string
�� 
query
�� $
=
��% &
strValue
��' /
;
��/ 0
if
�� 
(
�� 
query
�� !
.
��! "

StartsWith
��" ,
(
��, -
$str
��- 1
)
��1 2
)
��2 3
{
�� 
query
�� !
=
��" #
query
��$ )
.
��) *
	Substring
��* 3
(
��3 4
$num
��4 5
,
��5 6
context
��7 >
.
��> ?
gqlvalue
��? G
.
��G H
GetText
��H O
(
��O P
)
��P Q
.
��Q R
Length
��R X
-
��Y Z
$num
��[ \
)
��\ ]
;
��] ^
}
�� 
return
�� (
BuildEntityQueryExpression
�� 9
(
��9 :
query
��: ?
)
��? @
;
��@ A
}
�� 
var
�� !
argumentNonNullType
�� +
=
��, -
argType
��. 5
.
��5 6
IsNullableType
��6 D
(
��D E
)
��E F
?
��G H
Nullable
��I Q
.
��Q R
GetUnderlyingType
��R c
(
��c d
argType
��d k
)
��k l
:
��m n
argType
��o v
;
��v w
if
�� 
(
�� !
argumentNonNullType
�� +
.
��+ ,
GetTypeInfo
��, 7
(
��7 8
)
��8 9
.
��9 :
IsEnum
��: @
)
��@ A
{
�� 
var
�� 
enumName
�� $
=
��% &
strValue
��' /
;
��/ 0
var
�� 

valueIndex
�� &
=
��' (
Enum
��) -
.
��- .
GetNames
��. 6
(
��6 7!
argumentNonNullType
��7 J
)
��J K
.
��K L
ToList
��L R
(
��R S
)
��S T
.
��T U
	FindIndex
��U ^
(
��^ _
n
��_ `
=>
��a c
n
��d e
==
��f h
enumName
��i q
)
��q r
;
��r s
if
�� 
(
�� 

valueIndex
�� &
==
��' )
-
��* +
$num
��+ ,
)
��, -
{
�� 
throw
�� !
new
��" %,
EntityGraphQLCompilerException
��& D
(
��D E
$"
��E G
Value 
��G M
{
��M N
enumName
��N V
}
��V W)
 is not valid for argument 
��W r
{
��r s
context
��s z
.
��z {
gqlfield��{ �
}��� �
"��� �
)��� �
;��� �
}
�� 
var
�� 
	enumValue
�� %
=
��& '
Enum
��( ,
.
��, -
	GetValues
��- 6
(
��6 7!
argumentNonNullType
��7 J
)
��J K
.
��K L
GetValue
��L T
(
��T U

valueIndex
��U _
)
��_ `
;
��` a
return
�� 
(
��  
ExpressionResult
��  0
)
��0 1

Expression
��1 ;
.
��; <
Constant
��< D
(
��D E
	enumValue
��E N
)
��N O
;
��O P
}
�� 
}
�� 
}
�� 
return
�� 
gqlVarValue
�� 
;
�� 
}
�� 	
private
�� 
ExpressionResult
��  (
BuildEntityQueryExpression
��! ;
(
��; <
string
��< B
query
��C H
)
��H I
{
�� 	
var
�� 
prop
�� 
=
�� 
(
�� 
(
�� 
Schema
�� 
.
��  
Field
��  %
)
��% &"
fieldArgumentContext
��& :
)
��: ;
.
��; <!
ArgumentTypesObject
��< O
.
��O P
GetType
��P W
(
��W X
)
��X Y
.
��Y Z
GetProperties
��Z g
(
��g h
)
��h i
.
��i j
FirstOrDefault
��j x
(
��x y
p
��y z
=>
��{ }
p
��~ 
.�� �
PropertyType��� �
.��� �(
GetGenericTypeDefinition��� �
(��� �
)��� �
==��� �
typeof��� �
(��� �
EntityQueryType��� �
<��� �
>��� �
)��� �
)��� �
;��� �
var
�� 
eqlt
�� 
=
�� 
prop
�� 
.
�� 
GetValue
�� $
(
��$ %
(
��% &
(
��& '
Schema
��' -
.
��- .
Field
��. 3
)
��3 4"
fieldArgumentContext
��4 H
)
��H I
.
��I J!
ArgumentTypesObject
��J ]
)
��] ^
as
��_ a!
BaseEntityQueryType
��b u
;
��u v
var
�� 
contextParam
�� 
=
�� 

Expression
�� )
.
��) *
	Parameter
��* 3
(
��3 4
eqlt
��4 8
.
��8 9
	QueryType
��9 B
)
��B C
;
��C D
if
�� 
(
�� 
string
�� 
.
�� 
IsNullOrEmpty
�� $
(
��$ %
query
��% *
)
��* +
)
��+ ,
{
�� 
return
�� 
null
�� 
;
�� 
}
�� 
ExpressionResult
�� 
expressionResult
�� -
=
��. /
EqlCompiler
��0 ;
.
��; <
CompileWith
��< G
(
��G H
query
��H M
,
��M N
contextParam
��O [
,
��[ \
schemaProvider
��] k
,
��k l
methodProvider
��m {
,
��{ |
	variables��} �
)��� �
.��� � 
ExpressionResult��� �
;��� �
expressionResult
�� 
=
�� 
(
��  
ExpressionResult
��  0
)
��0 1

Expression
��1 ;
.
��; <
Lambda
��< B
(
��B C
expressionResult
��C S
.
��S T

Expression
��T ^
,
��^ _
contextParam
��` l
)
��l m
;
��m n
return
�� 
expressionResult
�� #
;
��# $
}
�� 	
private
�� 
ExpressionResult
��  !
MakeFieldExpression
��! 4
(
��4 5
string
��5 ;
field
��< A
,
��A B

Dictionary
��C M
<
��M N
string
��N T
,
��T U
ExpressionResult
��V f
>
��f g
args
��h l
)
��l m
{
�� 	
string
�� 
name
�� 
=
�� 
schemaProvider
�� (
.
��( )*
GetSchemaTypeNameForRealType
��) E
(
��E F
currentContext
��F T
.
��T U
Type
��U Y
)
��Y Z
;
��Z [
if
�� 
(
�� 
!
�� 
schemaProvider
�� 
.
��  
TypeHasField
��  ,
(
��, -
name
��- 1
,
��1 2
field
��3 8
,
��8 9
args
��: >
!=
��? A
null
��B F
?
��G H
args
��I M
.
��M N
Select
��N T
(
��T U
d
��U V
=>
��W Y
d
��Z [
.
��[ \
Key
��\ _
)
��_ `
:
��a b
new
��c f
string
��g m
[
��m n
$num
��n o
]
��o p
)
��p q
)
��q r
{
�� 
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 8
(
��8 9
$"
��9 ;
Field '
��; B
{
��B C
field
��C H
}
��H I.
 ' not found on current context '
��I i
{
��i j
name
��j n
}
��n o
'
��o p
"
��p q
)
��q r
;
��r s
}
�� 
var
�� 
exp
�� 
=
�� 
schemaProvider
�� $
.
��$ %#
GetExpressionForField
��% :
(
��: ;
currentContext
��; I
,
��I J
name
��K O
,
��O P
field
��Q V
,
��V W
args
��X \
)
��\ ]
;
��] ^
return
�� 
exp
�� 
;
�� 
}
�� 	
private
�� 
ExpressionResult
��  $
MakeMutationExpression
��! 7
(
��7 8
string
��8 >
method
��? E
,
��E F
MutationType
��G S
mutationType
��T `
,
��` a

Dictionary
��b l
<
��l m
string
��m s
,
��s t
ExpressionResult��u �
>��� �
args��� �
)��� �
{
�� 	
return
�� 
new
�� 
MutationResult
�� %
(
��% &
method
��& ,
,
��, -
mutationType
��. :
,
��: ;
args
��< @
)
��@ A
;
��A B
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
VisitInt
��) 1
(
��1 2!
EntityGraphQLParser
��2 E
.
��E F

IntContext
��F P
context
��Q X
)
��X Y
{
�� 	
string
�� 
s
�� 
=
�� 
context
�� 
.
�� 
GetText
�� &
(
��& '
)
��' (
;
��( )
return
�� 
(
�� 
ExpressionResult
�� $
)
��$ %
(
��% &
s
��& '
.
��' (

StartsWith
��( 2
(
��2 3
$str
��3 6
)
��6 7
?
��8 9

Expression
��: D
.
��D E
Constant
��E M
(
��M N
Int64
��N S
.
��S T
Parse
��T Y
(
��Y Z
s
��Z [
)
��[ \
)
��\ ]
:
��^ _

Expression
��` j
.
��j k
Constant
��k s
(
��s t
UInt64
��t z
.
��z {
Parse��{ �
(��� �
s��� �
)��� �
)��� �
)��� �
;��� �
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
VisitBoolean
��) 5
(
��5 6!
EntityGraphQLParser
��6 I
.
��I J
BooleanContext
��J X
context
��Y `
)
��` a
{
�� 	
string
�� 
s
�� 
=
�� 
context
�� 
.
�� 
GetText
�� &
(
��& '
)
��' (
;
��( )
return
�� 
(
�� 
ExpressionResult
�� $
)
��$ %

Expression
��% /
.
��/ 0
Constant
��0 8
(
��8 9
bool
��9 =
.
��= >
Parse
��> C
(
��C D
s
��D E
)
��E F
)
��F G
;
��G H
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
VisitDecimal
��) 5
(
��5 6!
EntityGraphQLParser
��6 I
.
��I J
DecimalContext
��J X
context
��Y `
)
��` a
{
�� 	
return
�� 
(
�� 
ExpressionResult
�� $
)
��$ %

Expression
��% /
.
��/ 0
Constant
��0 8
(
��8 9
Decimal
��9 @
.
��@ A
Parse
��A F
(
��F G
context
��G N
.
��N O
GetText
��O V
(
��V W
)
��W X
)
��X Y
)
��Y Z
;
��Z [
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
VisitString
��) 4
(
��4 5!
EntityGraphQLParser
��5 H
.
��H I
StringContext
��I V
context
��W ^
)
��^ _
{
�� 	
string
�� 
value
�� 
=
�� 
context
�� "
.
��" #
GetText
��# *
(
��* +
)
��+ ,
.
��, -
	Substring
��- 6
(
��6 7
$num
��7 8
,
��8 9
context
��: A
.
��A B
GetText
��B I
(
��I J
)
��J K
.
��K L
Length
��L R
-
��S T
$num
��U V
)
��V W
.
��W X
Replace
��X _
(
��_ `
$str
��` f
,
��f g
$str
��h l
)
��l m
;
��m n
var
�� 
exp
�� 
=
�� 
(
�� 
ExpressionResult
�� '
)
��' (

Expression
��( 2
.
��2 3
Constant
��3 ;
(
��; <
value
��< A
)
��A B
;
��B C
if
�� 
(
�� 
	guidRegex
�� 
.
�� 
IsMatch
�� !
(
��! "
value
��" '
)
��' (
)
��( )
exp
�� 
=
�� 
ConvertToGuid
�� #
(
��# $
exp
��$ '
)
��' (
;
��( )
return
�� 
exp
�� 
;
�� 
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
	VisitNull
��) 2
(
��2 3!
EntityGraphQLParser
��3 F
.
��F G
NullContext
��G R
context
��S Z
)
��Z [
{
�� 	
var
�� 
exp
�� 
=
�� 
(
�� 
ExpressionResult
�� '
)
��' (

Expression
��( 2
.
��2 3
Constant
��3 ;
(
��; <
null
��< @
)
��@ A
;
��A B
return
�� 
exp
�� 
;
�� 
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
VisitIfThenElse
��) 8
(
��8 9!
EntityGraphQLParser
��9 L
.
��L M
IfThenElseContext
��M ^
context
��_ f
)
��f g
{
�� 	
return
�� 
(
�� 
ExpressionResult
�� $
)
��$ %

Expression
��% /
.
��/ 0
	Condition
��0 9
(
��9 :"
CheckConditionalTest
��: N
(
��N O
Visit
��O T
(
��T U
context
��U \
.
��\ ]
test
��] a
)
��a b
)
��b c
,
��c d
Visit
��e j
(
��j k
context
��k r
.
��r s
ifTrue
��s y
)
��y z
,
��z {
Visit��| �
(��� �
context��� �
.��� �
ifFalse��� �
)��� �
)��� �
;��� �
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (#
VisitIfThenElseInline
��) >
(
��> ?!
EntityGraphQLParser
��? R
.
��R S%
IfThenElseInlineContext
��S j
context
��k r
)
��r s
{
�� 	
return
�� 
(
�� 
ExpressionResult
�� $
)
��$ %

Expression
��% /
.
��/ 0
	Condition
��0 9
(
��9 :"
CheckConditionalTest
��: N
(
��N O
Visit
��O T
(
��T U
context
��U \
.
��\ ]
test
��] a
)
��a b
)
��b c
,
��c d
Visit
��e j
(
��j k
context
��k r
.
��r s
ifTrue
��s y
)
��y z
,
��z {
Visit��| �
(��� �
context��� �
.��� �
ifFalse��� �
)��� �
)��� �
;��� �
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
	VisitCall
��) 2
(
��2 3!
EntityGraphQLParser
��3 F
.
��F G
CallContext
��G R
context
��S Z
)
��Z [
{
�� 	
var
�� 
method
�� 
=
�� 
context
��  
.
��  !
method
��! '
.
��' (
GetText
��( /
(
��/ 0
)
��0 1
;
��1 2
if
�� 
(
�� 
!
�� 
methodProvider
�� 
.
��  !
EntityTypeHasMethod
��  3
(
��3 4
currentContext
��4 B
.
��B C
Type
��C G
,
��G H
method
��I O
)
��O P
)
��P Q
{
�� 
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 8
(
��8 9
$"
��9 ;
Method '
��; C
{
��C D
method
��D J
}
��J K.
 ' not found on current context '
��K k
{
��k l
currentContext
��l z
.
��z {
Type
��{ 
.�� �
Name��� �
}��� �
'��� �
"��� �
)��� �
;��� �
}
�� 
var
�� 
outerContext
�� 
=
�� 
currentContext
�� -
;
��- .
var
�� 
methodArgContext
��  
=
��! "
methodProvider
��# 1
.
��1 2
GetMethodContext
��2 B
(
��B C
currentContext
��C Q
,
��Q R
method
��S Y
)
��Y Z
;
��Z [
currentContext
�� 
=
�� 
methodArgContext
�� -
;
��- .
var
�� 
args
�� 
=
�� 
context
�� 
.
�� 
	arguments
�� (
?
��( )
.
��) *
children
��* 2
.
��2 3
Select
��3 9
(
��9 :
c
��: ;
=>
��< >
Visit
��? D
(
��D E
c
��E F
)
��F G
)
��G H
.
��H I
ToList
��I O
(
��O P
)
��P Q
;
��Q R
var
�� 
call
�� 
=
�� 
methodProvider
�� %
.
��% &
MakeCall
��& .
(
��. /
outerContext
��/ ;
,
��; <
methodArgContext
��= M
,
��M N
method
��O U
,
��U V
args
��W [
)
��[ \
;
��\ ]
currentContext
�� 
=
�� 
call
�� !
;
��! "
return
�� 
call
�� 
;
�� 
}
�� 	
public
�� 
override
�� 
ExpressionResult
�� (
	VisitArgs
��) 2
(
��2 3!
EntityGraphQLParser
��3 F
.
��F G
ArgsContext
��G R
context
��S Z
)
��Z [
{
�� 	
return
�� 
VisitChildren
��  
(
��  !
context
��! (
)
��( )
;
��) *
}
�� 	
private
�� 
ExpressionResult
��   
ConvertLeftOrRight
��! 3
(
��3 4
ExpressionType
��4 B
op
��C E
,
��E F
ExpressionResult
��G W
left
��X \
,
��\ ]
ExpressionResult
��^ n
right
��o t
)
��t u
{
�� 	
if
�� 
(
�� 
left
�� 
.
�� 
Type
�� 
.
�� 
IsNullableType
�� (
(
��( )
)
��) *
&&
��+ -
!
��. /
right
��/ 4
.
��4 5
Type
��5 9
.
��9 :
IsNullableType
��: H
(
��H I
)
��I J
)
��J K
right
�� 
=
�� 
(
�� 
ExpressionResult
�� )
)
��) *

Expression
��* 4
.
��4 5
Convert
��5 <
(
��< =
right
��= B
,
��B C
left
��D H
.
��H I
Type
��I M
)
��M N
;
��N O
else
�� 
if
�� 
(
�� 
right
�� 
.
�� 
Type
�� 
.
��  
IsNullableType
��  .
(
��. /
)
��/ 0
&&
��1 3
!
��4 5
left
��5 9
.
��9 :
Type
��: >
.
��> ?
IsNullableType
��? M
(
��M N
)
��N O
)
��O P
left
�� 
=
�� 
(
�� 
ExpressionResult
�� (
)
��( )

Expression
��) 3
.
��3 4
Convert
��4 ;
(
��; <
left
��< @
,
��@ A
right
��B G
.
��G H
Type
��H L
)
��L M
;
��M N
else
�� 
if
�� 
(
�� 
left
�� 
.
�� 
Type
�� 
==
�� !
typeof
��" (
(
��( )
int
��) ,
)
��, -
&&
��. 0
(
��1 2
right
��2 7
.
��7 8
Type
��8 <
==
��= ?
typeof
��@ F
(
��F G
uint
��G K
)
��K L
||
��M O
right
��P U
.
��U V
Type
��V Z
==
��[ ]
typeof
��^ d
(
��d e
Int16
��e j
)
��j k
||
��l n
right
��o t
.
��t u
Type
��u y
==
��z |
typeof��} �
(��� �
Int64��� �
)��� �
||��� �
right��� �
.��� �
Type��� �
==��� �
typeof��� �
(��� �
UInt16��� �
)��� �
||��� �
right��� �
.��� �
Type��� �
==��� �
typeof��� �
(��� �
UInt64��� �
)��� �
)��� �
)��� �
right
�� 
=
�� 
(
�� 
ExpressionResult
�� )
)
��) *

Expression
��* 4
.
��4 5
Convert
��5 <
(
��< =
right
��= B
,
��B C
left
��D H
.
��H I
Type
��I M
)
��M N
;
��N O
else
�� 
if
�� 
(
�� 
left
�� 
.
�� 
Type
�� 
==
�� !
typeof
��" (
(
��( )
uint
��) -
)
��- .
&&
��/ 1
(
��2 3
right
��3 8
.
��8 9
Type
��9 =
==
��> @
typeof
��A G
(
��G H
int
��H K
)
��K L
||
��M O
right
��P U
.
��U V
Type
��V Z
==
��[ ]
typeof
��^ d
(
��d e
Int16
��e j
)
��j k
||
��l n
right
��o t
.
��t u
Type
��u y
==
��z |
typeof��} �
(��� �
Int64��� �
)��� �
||��� �
right��� �
.��� �
Type��� �
==��� �
typeof��� �
(��� �
UInt16��� �
)��� �
||��� �
right��� �
.��� �
Type��� �
==��� �
typeof��� �
(��� �
UInt64��� �
)��� �
)��� �
)��� �
left
�� 
=
�� 
(
�� 
ExpressionResult
�� (
)
��( )

Expression
��) 3
.
��3 4
Convert
��4 ;
(
��; <
left
��< @
,
��@ A
right
��B G
.
��G H
Type
��H L
)
��L M
;
��M N
return
�� 
(
�� 
ExpressionResult
�� $
)
��$ %

Expression
��% /
.
��/ 0

MakeBinary
��0 :
(
��: ;
op
��; =
,
��= >
left
��? C
,
��C D
right
��E J
)
��J K
;
��K L
}
�� 	
private
�� 

Expression
�� "
CheckConditionalTest
�� /
(
��/ 0

Expression
��0 :
test
��; ?
)
��? @
{
�� 	
if
�� 
(
�� 
test
�� 
.
�� 
Type
�� 
!=
�� 
typeof
�� #
(
��# $
bool
��$ (
)
��( )
)
��) *
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 8
(
��8 9
$"
��9 ;D
6Expected boolean value in conditional test but found '
��; q
{
��q r
test
��r v
}
��v w
'
��w x
"
��x y
)
��y z
;
��z {
return
�� 
test
�� 
;
�� 
}
�� 	
private
�� 
ExpressionType
�� 
MakeOperator
�� +
(
��+ ,
string
��, 2
op
��3 5
)
��5 6
{
�� 	
switch
�� 
(
�� 
op
�� 
)
�� 
{
�� 
case
�� 
$str
�� 
:
�� 
return
��  
ExpressionType
��! /
.
��/ 0
Equal
��0 5
;
��5 6
case
�� 
$str
�� 
:
�� 
return
��  
ExpressionType
��! /
.
��/ 0
Add
��0 3
;
��3 4
case
�� 
$str
�� 
:
�� 
return
��  
ExpressionType
��! /
.
��/ 0
Subtract
��0 8
;
��8 9
case
�� 
$str
�� 
:
�� 
return
��  
ExpressionType
��! /
.
��/ 0
Modulo
��0 6
;
��6 7
case
�� 
$str
�� 
:
�� 
return
��  
ExpressionType
��! /
.
��/ 0
Power
��0 5
;
��5 6
case
�� 
$str
�� 
:
�� 
return
�� "
ExpressionType
��# 1
.
��1 2
AndAlso
��2 9
;
��9 :
case
�� 
$str
�� 
:
�� 
return
��  
ExpressionType
��! /
.
��/ 0
Multiply
��0 8
;
��8 9
case
�� 
$str
�� 
:
�� 
return
�� !
ExpressionType
��" 0
.
��0 1
OrElse
��1 7
;
��7 8
case
�� 
$str
�� 
:
�� 
return
�� !
ExpressionType
��" 0
.
��0 1
LessThanOrEqual
��1 @
;
��@ A
case
�� 
$str
�� 
:
�� 
return
�� !
ExpressionType
��" 0
.
��0 1 
GreaterThanOrEqual
��1 C
;
��C D
case
�� 
$str
�� 
:
�� 
return
��  
ExpressionType
��! /
.
��/ 0
LessThan
��0 8
;
��8 9
case
�� 
$str
�� 
:
�� 
return
��  
ExpressionType
��! /
.
��/ 0
GreaterThan
��0 ;
;
��; <
default
�� 
:
�� 
throw
�� 
new
�� ",
EntityGraphQLCompilerException
��# A
(
��A B
$"
��B D+
Unsupported binary operator '
��D a
{
��a b
op
��b d
}
��d e
'
��e f
"
��f g
)
��g h
;
��h i
}
�� 
}
�� 	
}
�� 
}�� �	
NY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\Util\BaseIdentityFinder.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
.  !
Util! %
{ 
internal 
class 
BaseIdentityFinder %
:& '$
EntityGraphQLBaseVisitor( @
<@ A
stringA G
>G H
{ 
public 
override 
string 
VisitIdentity ,
(, -
EntityGraphQLParser- @
.@ A
IdentityContextA P
contextQ X
)X Y
{ 	
return		 
context		 
.		 
GetText		 "
(		" #
)		# $
;		$ %
}

 	
public 
override 
string 
VisitGqlcall +
(+ ,
EntityGraphQLParser, ?
.? @
GqlcallContext@ N
contextO V
)V W
{ 	
return 
context 
. 
method !
.! "
GetText" )
() *
)* +
;+ ,
} 	
} 
} Ԡ
JY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\Util\ExpressionUtil.cs
	namespace		 	
EntityGraphQL		
 
.		 
Compiler		  
.		  !
Util		! %
{

 
public 

class 
ExpressionUtil 
{ 
public 
static 
ExpressionResult &
MakeExpressionCall' 9
(9 :
Type: >
[> ?
]? @
typesA F
,F G
stringH N

methodNameO Y
,Y Z
Type[ _
[_ `
]` a
genericTypesb n
,n o
paramsp v

Expression	w �
[
� �
]
� �

parameters
� �
)
� �
{ 	
foreach 
( 
var 
t 
in 
types #
)# $
{ 
try 
{ 
return 
( 
ExpressionResult ,
), -

Expression- 7
.7 8
Call8 <
(< =
t= >
,> ?

methodName@ J
,J K
genericTypesL X
,X Y

parametersZ d
)d e
;e f
} 
catch 
( %
InvalidOperationException 0
)0 1
{ 
continue 
; 
} 
} 
var 
typesStr 
= 
string !
.! "
Join" &
<& '
Type' +
>+ ,
(, -
$str- 1
,1 2
types3 8
)8 9
;9 :
throw 
new *
EntityGraphQLCompilerException 4
(4 5
$"5 7,
 Could not find extension method 7 W
{W X

methodNameX b
}b c

 on types c m
{m n
typesStrn v
}v w
"w x
)x y
;y z
} 	
public 
static 
MemberExpression &'
CheckAndGetMemberExpression' B
<B C
	TBaseTypeC L
,L M
TReturnN U
>U V
(V W

ExpressionW a
<a b
Funcb f
<f g
	TBaseTypeg p
,p q
TReturnr y
>y z
>z {
fieldSelection	| �
)
� �
{   	
var!! 
exp!! 
=!! 
fieldSelection!! $
.!!$ %
Body!!% )
;!!) *
if"" 
("" 
exp"" 
."" 
NodeType"" 
=="" 
ExpressionType""  .
."". /
Convert""/ 6
)""6 7
exp## 
=## 
(## 
(## 
UnaryExpression## '
)##' (
exp##( +
)##+ ,
.##, -
Operand##- 4
;##4 5
if%% 
(%% 
exp%% 
.%% 
NodeType%% 
!=%% 
ExpressionType%%  .
.%%. /
MemberAccess%%/ ;
)%%; <
throw&& 
new&& 
ArgumentException&& +
(&&+ ,
$str	&&, �
,
&&� �
$str
&&� �
)
&&� �
;
&&� �
return'' 
('' 
MemberExpression'' $
)''$ %
exp''% (
;''( )
}(( 	
public** 
static** 
object** 

ChangeType** '
(**' (
object**( .
value**/ 4
,**4 5
Type**6 :
type**; ?
)**? @
{++ 	
var,, 
objType,, 
=,, 
value,, 
.,,  
GetType,,  '
(,,' (
),,( )
;,,) *
if-- 
(-- 
typeof-- 
(-- 

Newtonsoft-- !
.--! "
Json--" &
.--& '
Linq--' +
.--+ ,
JToken--, 2
)--2 3
.--3 4
IsAssignableFrom--4 D
(--D E
objType--E L
)--L M
)--M N
{--O P
var.. 
newVal.. 
=.. 
(.. 
(.. 

Newtonsoft.. )
...) *
Json..* .
.... /
Linq../ 3
...3 4
JToken..4 :
)..: ;
value..; @
)..@ A
...A B
ToObject..B J
(..J K
type..K O
)..O P
;..P Q
return// 
newVal// 
;// 
}00 
if22 
(22 
type22 
!=22 
typeof22 
(22 
string22 %
)22% &
&&22' )
objType22* 1
==222 4
typeof225 ;
(22; <
string22< B
)22B C
)22C D
{22E F
if33 
(33 
type33 
==33 
typeof33 "
(33" #
double33# )
)33) *
||33+ -
type33. 2
==333 5
typeof336 <
(33< =
Nullable33= E
<33E F
double33F L
>33L M
)33M N
)33N O
return44 
double44 !
.44! "
Parse44" '
(44' (
(44( )
string44) /
)44/ 0
value440 5
)445 6
;446 7
if55 
(55 
type55 
==55 
typeof55 "
(55" #
float55# (
)55( )
||55* ,
type55- 1
==552 4
typeof555 ;
(55; <
Nullable55< D
<55D E
float55E J
>55J K
)55K L
)55L M
return66 
float66  
.66  !
Parse66! &
(66& '
(66' (
string66( .
)66. /
value66/ 4
)664 5
;665 6
if77 
(77 
type77 
==77 
typeof77 "
(77" #
int77# &
)77& '
||77( *
type77+ /
==770 2
typeof773 9
(779 :
Nullable77: B
<77B C
int77C F
>77F G
)77G H
)77H I
return88 
int88 
.88 
Parse88 $
(88$ %
(88% &
string88& ,
)88, -
value88- 2
)882 3
;883 4
if99 
(99 
type99 
==99 
typeof99 "
(99" #
uint99# '
)99' (
||99) +
type99, 0
==991 3
typeof994 :
(99: ;
Nullable99; C
<99C D
uint99D H
>99H I
)99I J
)99J K
return:: 
uint:: 
.::  
Parse::  %
(::% &
(::& '
string::' -
)::- .
value::. 3
)::3 4
;::4 5
};; 
var<< 
argumentNonNullType<< #
=<<$ %
type<<& *
.<<* +
IsNullableType<<+ 9
(<<9 :
)<<: ;
?<<< =
Nullable<<> F
.<<F G
GetUnderlyingType<<G X
(<<X Y
type<<Y ]
)<<] ^
:<<_ `
type<<a e
;<<e f
var== 
valueNonNullType==  
===! "
objType==# *
.==* +
IsNullableType==+ 9
(==9 :
)==: ;
?==< =
Nullable==> F
.==F G
GetUnderlyingType==G X
(==X Y
objType==Y `
)==` a
:==b c
objType==d k
;==k l
if>> 
(>> 
argumentNonNullType>> #
.>># $
GetTypeInfo>>$ /
(>>/ 0
)>>0 1
.>>1 2
IsEnum>>2 8
)>>8 9
{?? 
return@@ 
Enum@@ 
.@@ 
ToObject@@ $
(@@$ %
argumentNonNullType@@% 8
,@@8 9
value@@: ?
)@@? @
;@@@ A
}AA 
ifBB 
(BB 
argumentNonNullTypeBB #
!=BB$ &
valueNonNullTypeBB' 7
)BB7 8
{CC 
varDD 
newValDD 
=DD 
ConvertDD $
.DD$ %

ChangeTypeDD% /
(DD/ 0
valueDD0 5
,DD5 6
argumentNonNullTypeDD7 J
)DDJ K
;DDK L
returnEE 
newValEE 
;EE 
}FF 
returnGG 
valueGG 
;GG 
}HH 	
publicPP 
staticPP 

ExpressionPP  
CombineExpressionsPP! 3
(PP3 4

ExpressionPP4 >
baseExpPP? F
,PPF G

ExpressionPPH R
nextExpPPS Z
)PPZ [
{QQ 	
switchRR 
(RR 
nextExpRR 
.RR 
NodeTypeRR $
)RR$ %
{SS 
caseTT 
ExpressionTypeTT #
.TT# $
CallTT$ (
:TT( )
{TT* +
varUU 
mcUU 
=UU 
(UU  
MethodCallExpressionUU 2
)UU2 3
nextExpUU3 :
;UU: ;
ifVV 
(VV 
mcVV 
.VV 
ObjectVV !
==VV" $
nullVV% )
)VV) *
{WW 
varXX 
argsXX  
=XX! "
newXX# &
ListXX' +
<XX+ ,

ExpressionXX, 6
>XX6 7
{XX8 9
baseExpXX: A
}XXB C
;XXC D
varYY 
newParamYY $
=YY% &

ExpressionYY' 1
.YY1 2
	ParameterYY2 ;
(YY; <
baseExpYY< C
.YYC D
TypeYYD H
.YYH I
GetGenericArgumentsYYI \
(YY\ ]
)YY] ^
.YY^ _
FirstYY_ d
(YYd e
)YYe f
)YYf g
;YYg h
foreachZZ 
(ZZ  !
varZZ! $
itemZZ% )
inZZ* ,
mcZZ- /
.ZZ/ 0
	ArgumentsZZ0 9
.ZZ9 :
SkipZZ: >
(ZZ> ?
$numZZ? @
)ZZ@ A
)ZZA B
{[[ 
var\\ 
lambda\\  &
=\\' (
(\\) *
LambdaExpression\\* :
)\\: ;
item\\; ?
;\\? @
var]] 
exp]]  #
=]]$ %
new]]& )
ParameterReplacer]]* ;
(]]; <
)]]< =
.]]= >
Replace]]> E
(]]E F
lambda]]F L
,]]L M
lambda]]N T
.]]T U

Parameters]]U _
.]]_ `
First]]` e
(]]e f
)]]f g
,]]g h
newParam]]i q
)]]q r
;]]r s
args^^  
.^^  !
Add^^! $
(^^$ %
exp^^% (
)^^( )
;^^) *
}__ 
var`` 
call``  
=``! "
ExpressionUtil``# 1
.``1 2
MakeExpressionCall``2 D
(``D E
new``E H
[``H I
]``I J
{``K L
typeof``M S
(``S T
	Queryable``T ]
)``] ^
,``^ _
typeof``` f
(``f g

Enumerable``g q
)``q r
}``s t
,``t u
mc``v x
.``x y
Method``y 
.	`` �
Name
``� �
,
``� �
baseExp
``� �
.
``� �
Type
``� �
.
``� �!
GetGenericArguments
``� �
(
``� �
)
``� �
.
``� �
ToArray
``� �
(
``� �
)
``� �
,
``� �
args
``� �
.
``� �
ToArray
``� �
(
``� �
)
``� �
)
``� �
;
``� �
returnaa 
callaa #
;aa# $
}bb 
returncc 

Expressioncc %
.cc% &
Callcc& *
(cc* +
baseExpcc+ 2
,cc2 3
mccc4 6
.cc6 7
Methodcc7 =
,cc= >
mccc? A
.ccA B
	ArgumentsccB K
)ccK L
;ccL M
}dd 
defaultee 
:ee 
throwee 
newee "*
EntityGraphQLCompilerExceptionee# A
(eeA B
$"eeB D(
Could not join expressions 'eeD `
{ee` a
baseExpeea h
.eeh i
NodeTypeeei q
}eeq r
 and 'eer x
{eex y
nextExp	eey �
.
ee� �
NodeType
ee� �
}
ee� �
'
ee� �
"
ee� �
)
ee� �
;
ee� �
}ff 
}gg 	
publicnn 
staticnn 
Tuplenn 
<nn 

Expressionnn &
,nn& '

Expressionnn( 2
>nn2 3
FindIEnumerablenn4 C
(nnC D

ExpressionnnD N
baseExpressionnnO ]
)nn] ^
{oo 	
varpp 
exppp 
=pp 
baseExpressionpp $
;pp$ %

Expressionqq 
endExpressionqq $
=qq% &
nullqq' +
;qq+ ,
whilerr 
(rr 
exprr 
!=rr 
nullrr 
&&rr !
!rr" #
exprr# &
.rr& '
Typerr' +
.rr+ ,
IsEnumerableOrArrayrr, ?
(rr? @
)rr@ A
)rrA B
{ss 
switchtt 
(tt 
exptt 
.tt 
NodeTypett $
)tt$ %
{uu 
casevv 
ExpressionTypevv '
.vv' (
Callvv( ,
:vv, -
{vv. /
endExpressionww %
=ww& '
expww( +
;ww+ ,
varxx 
mcxx 
=xx  
(xx! " 
MethodCallExpressionxx" 6
)xx6 7
expxx7 :
;xx: ;
expyy 
=yy 
mcyy  
.yy  !
Objectyy! '
!=yy( *
nullyy+ /
?yy0 1
mcyy2 4
.yy4 5
Objectyy5 ;
:yy< =
mcyy> @
.yy@ A
	ArgumentsyyA J
.yyJ K
FirstyyK P
(yyP Q
)yyQ R
;yyR S
breakzz 
;zz 
}{{ 
default|| 
:|| 
exp||  
=||! "
null||# '
;||' (
break}} 
;}} 
}~~ 
} 
return
�� 
Tuple
�� 
.
�� 
Create
�� 
(
��  
exp
��  #
,
��# $
endExpression
��% 2
)
��2 3
;
��3 4
}
�� 	
public
�� 
static
�� 

Expression
��  !
SelectDynamicToList
��! 4
(
��4 5!
ParameterExpression
��5 H!
currentContextParam
��I \
,
��\ ]

Expression
��^ h
baseExp
��i p
,
��p q
IEnumerable
��r }
<
��} ~
IGraphQLNode��~ �
>��� � 
fieldExpressions��� �
,��� �
ISchemaProvider��� �
schemaProvider��� �
)��� �
{
�� 	
Type
�� 
dynamicType
�� 
;
�� 
var
�� 

memberInit
�� 
=
�� !
CreateNewExpression
�� 0
(
��0 1!
currentContextParam
��1 D
,
��D E
fieldExpressions
��F V
,
��V W
schemaProvider
��X f
,
��f g
out
��h k
dynamicType
��l w
)
��w x
;
��x y
var
�� 
selector
�� 
=
�� 

Expression
�� %
.
��% &
Lambda
��& ,
(
��, -

memberInit
��- 7
,
��7 8!
currentContextParam
��9 L
)
��L M
;
��M N
var
�� 
call
�� 
=
�� 
ExpressionUtil
�� %
.
��% & 
MakeExpressionCall
��& 8
(
��8 9
new
��9 <
[
��= >
]
��> ?
{
��@ A
typeof
��A G
(
��G H
	Queryable
��H Q
)
��Q R
,
��R S
typeof
��T Z
(
��Z [

Enumerable
��[ e
)
��e f
}
��f g
,
��g h
$str
��i q
,
��q r
new
��s v
Type
��w {
[
��{ |
$num
��| }
]
��} ~
{�� �#
currentContextParam��� �
.��� �
Type��� �
,��� �
dynamicType��� �
}��� �
,��� �
baseExp��� �
,��� �
selector��� �
)��� �
;��� �
return
�� 
call
�� 
;
�� 
}
�� 	
public
�� 
static
�� 

Expression
��  !
CreateNewExpression
��! 4
(
��4 5

Expression
��5 ?
currentContext
��@ N
,
��N O
IEnumerable
��P [
<
��[ \
IGraphQLNode
��\ h
>
��h i
fieldExpressions
��j z
,
��z {
ISchemaProvider��| �
schemaProvider��� �
)��� �
{
�� 	
Type
�� 
dynamicType
�� 
;
�� 
var
�� 

memberInit
�� 
=
�� !
CreateNewExpression
�� 0
(
��0 1
currentContext
��1 ?
,
��? @
fieldExpressions
��A Q
,
��Q R
schemaProvider
��S a
,
��a b
out
��c f
dynamicType
��g r
)
��r s
;
��s t
return
�� 

memberInit
�� 
;
�� 
}
�� 	
private
�� 
static
�� 

Expression
�� !!
CreateNewExpression
��" 5
(
��5 6

Expression
��6 @
currentContext
��A O
,
��O P
IEnumerable
��Q \
<
��\ ]
IGraphQLNode
��] i
>
��i j
fieldExpressions
��k {
,
��{ |
ISchemaProvider��} �
schemaProvider��� �
,��� �
out��� �
Type��� �
dynamicType��� �
)��� �
{
�� 	
var
�� $
fieldExpressionsByName
�� &
=
��' (
new
��) ,

Dictionary
��- 7
<
��7 8
String
��8 >
,
��> ?
ExpressionResult
��@ P
>
��P Q
(
��Q R
)
��R S
;
��S T
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��  
fieldExpressions
��! 1
)
��1 2
{
�� $
fieldExpressionsByName
�� &
[
��& '
item
��' +
.
��+ ,
Name
��, 0
]
��0 1
=
��2 3
item
��4 8
.
��8 9
NodeExpression
��9 G
;
��G H
}
�� 
dynamicType
�� 
=
�� $
LinqRuntimeTypeBuilder
�� 0
.
��0 1
GetDynamicType
��1 ?
(
��? @$
fieldExpressionsByName
��@ V
.
��V W
ToDictionary
��W c
(
��c d
f
��d e
=>
��f h
f
��i j
.
��j k
Key
��k n
,
��n o
f
��p q
=>
��r t
f
��u v
.
��v w
Value
��w |
.
��| }
Type��} �
)��� �
)��� �
;��� �
var
�� 
bindings
�� 
=
�� 
dynamicType
�� &
.
��& '
	GetFields
��' 0
(
��0 1
)
��1 2
.
��2 3
Select
��3 9
(
��9 :
p
��: ;
=>
��< >

Expression
��? I
.
��I J
Bind
��J N
(
��N O
p
��O P
,
��P Q$
fieldExpressionsByName
��R h
[
��h i
p
��i j
.
��j k
Name
��k o
]
��o p
)
��p q
)
��q r
.
��r s
OfType
��s y
<
��y z
MemberBinding��z �
>��� �
(��� �
)��� �
;��� �
var
�� 
newExp
�� 
=
�� 

Expression
�� #
.
��# $
New
��$ '
(
��' (
dynamicType
��( 3
.
��3 4
GetConstructor
��4 B
(
��B C
Type
��C G
.
��G H

EmptyTypes
��H R
)
��R S
)
��S T
;
��T U
var
�� 
mi
�� 
=
�� 

Expression
�� 
.
��  

MemberInit
��  *
(
��* +
newExp
��+ 1
,
��1 2
bindings
��3 ;
)
��; <
;
��< =
return
�� 
mi
�� 
;
�� 
}
�� 	
}
�� 
}�� �F
RY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\Util\LinqRuntimeTypeBuilder.cs
	namespace		 	
EntityGraphQL		
 
.		 
Compiler		  
.		  !
Util		! %
{

 
public 

static 
class "
LinqRuntimeTypeBuilder .
{ 
private 
static 
AssemblyName #
_assemblyName$ 1
=2 3
new4 7
AssemblyName8 D
(D E
)E F
{G H
NameI M
=N O
$strP b
}c d
;d e
private 
static 
ModuleBuilder $
_moduleBuilder% 3
=4 5
null6 :
;: ;
private 
static 

Dictionary !
<! "
string" (
,( )
Type* .
>. /

builtTypes0 :
=; <
new= @

DictionaryA K
<K L
stringL R
,R S
TypeT X
>X Y
(Y Z
)Z [
;[ \
private 
static 

Dictionary !
<! "
string" (
,( )
string* 0
>0 1
typesByName2 =
=> ?
new@ C

DictionaryD N
<N O
stringO U
,U V
stringW ]
>] ^
(^ _
)_ `
;` a
static "
LinqRuntimeTypeBuilder %
(% &
)& '
{ 	
_moduleBuilder 
= 
AssemblyBuilder ,
., -!
DefineDynamicAssembly- B
(B C
_assemblyNameC P
,P Q!
AssemblyBuilderAccessR g
.g h
Runh k
)k l
.l m 
DefineDynamicModule	m �
(
� �
_assemblyName
� �
.
� �
Name
� �
)
� �
;
� �
} 	
private 
static 
string 

GetTypeKey (
(( )

Dictionary) 3
<3 4
string4 :
,: ;
Type< @
>@ A
fieldsB H
)H I
{ 	
string 
key 
= 
string 
.  
Empty  %
;% &
foreach 
( 
var 
field 
in !
fields" (
)( )
{   
key!! 
=!! 
MakeKey!! 
(!! 
key!! !
,!!! "
field!!# (
.!!( )
Key!!) ,
,!!, -
field!!. 3
.!!3 4
Value!!4 9
)!!9 :
;!!: ;
}"" 
return$$ 
$"$$ 
anon.$$ 
{$$ 
key$$ 
}$$ 
"$$  
;$$  !
}%% 	
private'' 
static'' 
string'' 
MakeKey'' %
(''% &
string''& ,
key''- 0
,''0 1
string''2 8
	fieldName''9 B
,''B C
Type''D H
	fieldType''I R
)''R S
{(( 	
string)) 
type)) 
;)) 
if** 
(** 
	fieldType** 
.** 
IsNullableType** (
(**( )
)**) *
)*** +
type++ 
=++ 
$str++ 
+++ 
	fieldType++ &
.++& '
GetGenericArguments++' :
(++: ;
)++; <
[++< =
$num++= >
]++> ?
.++? @
Name++@ D
;++D E
else,, 
if,, 
(,, 
	fieldType,, 
.,, 
IsEnumerableOrArray,, 2
(,,2 3
),,3 4
),,4 5
type-- 
=-- 
$str-- 
+-- 
	fieldType-- &
.--& '$
GetEnumerableOrArrayType--' ?
(--? @
)--@ A
.--A B
Name--B F
;--F G
else.. 
if.. 
(.. 
	fieldType.. 
... 
GetTypeInfo.. *
(..* +
)..+ ,
..., -
IsGenericType..- :
)..: ;
type// 
=// 
$"// 
{// 
	fieldType// #
.//# $
Name//$ (
}//( )
://) *
{//* +
string//+ 1
.//1 2
Join//2 6
(//6 7
$str//7 :
,//: ;
	fieldType//< E
.//E F
GetGenericArguments//F Y
(//Y Z
)//Z [
.//[ \
Select//\ b
(//b c
a//c d
=>//e g
a//h i
.//i j
Name//j n
)//n o
)//o p
}//p q
"//q r
;//r s
else00 
type11 
=11 
	fieldType11  
.11  !
Name11! %
;11% &
key33 
+=33 
	fieldName33 
+33 
type33 #
;33# $
return44 
key44 
;44 
}55 	
public77 
static77 
Type77 
GetDynamicType77 )
(77) *

Dictionary77* 4
<774 5
string775 ;
,77; <
Type77= A
>77A B
fields77C I
)77I J
{88 	
if99 
(99 
null99 
==99 
fields99 
)99 
throw:: 
new:: !
ArgumentNullException:: /
(::/ 0
$str::0 8
)::8 9
;::9 :
if;; 
(;; 
$num;; 
==;; 
fields;; 
.;; 
Count;; !
);;! "
throw<< 
new<< '
ArgumentOutOfRangeException<< 5
(<<5 6
$str<<6 >
,<<> ?
$str<<@ n
)<<n o
;<<o p
try>> 
{?? 
Monitor@@ 
.@@ 
Enter@@ 
(@@ 

builtTypes@@ (
)@@( )
;@@) *
stringAA 
	classNameAA  
=AA! "

GetTypeKeyAA# -
(AA- .
fieldsAA. 4
)AA4 5
;AA5 6
ifBB 
(BB 
!BB 
typesByNameBB  
.BB  !
ContainsKeyBB! ,
(BB, -
	classNameBB- 6
)BB6 7
)BB7 8
{CC 
typesByNameDD 
[DD  
	classNameDD  )
]DD) *
=DD+ ,
GuidDD- 1
.DD1 2
NewGuidDD2 9
(DD9 :
)DD: ;
.DD; <
ToStringDD< D
(DDD E
)DDE F
;DDF G
}EE 
varFF 
classIdFF 
=FF 
typesByNameFF )
[FF) *
	classNameFF* 3
]FF3 4
;FF4 5
ifHH 
(HH 

builtTypesHH 
.HH 
ContainsKeyHH *
(HH* +
classIdHH+ 2
)HH2 3
)HH3 4
returnII 

builtTypesII %
[II% &
classIdII& -
]II- .
;II. /
varKK 
typeBuilderKK 
=KK  !
_moduleBuilderKK" 0
.KK0 1

DefineTypeKK1 ;
(KK; <
classIdKK< C
,KKC D
TypeAttributesKKE S
.KKS T
PublicKKT Z
|KK[ \
TypeAttributesKK] k
.KKk l
ClassKKl q
|KKr s
TypeAttributes	KKt �
.
KK� �
Serializable
KK� �
)
KK� �
;
KK� �
foreachMM 
(MM 
varMM 
fieldMM "
inMM# %
fieldsMM& ,
)MM, -
{NN 
varOO 
fieldBuilderOO $
=OO% &
typeBuilderOO' 2
.OO2 3
DefineFieldOO3 >
(OO> ?
fieldOO? D
.OOD E
KeyOOE H
,OOH I
fieldOOJ O
.OOO P
ValueOOP U
,OOU V
FieldAttributesOOW f
.OOf g
PublicOOg m
)OOm n
;OOn o
}PP 

builtTypesRR 
[RR 
classIdRR "
]RR" #
=RR$ %
typeBuilderRR& 1
.RR1 2
CreateTypeInfoRR2 @
(RR@ A
)RRA B
.RRB C
AsTypeRRC I
(RRI J
)RRJ K
;RRK L
returnSS 

builtTypesSS !
[SS! "
classIdSS" )
]SS) *
;SS* +
}TT 
catchUU 
(UU 
	ExceptionUU 
exUU 
)UU  
{VV 
SystemWW 
.WW 
ConsoleWW 
.WW 
	WriteLineWW (
(WW( )
exWW) +
)WW+ ,
;WW, -
}XX 
finallyYY 
{ZZ 
Monitor[[ 
.[[ 
Exit[[ 
([[ 

builtTypes[[ '
)[[' (
;[[( )
}\\ 
return^^ 
null^^ 
;^^ 
}__ 	
}`` 
}aa �)
MY:\Develop\EntityGraphQL\src\EntityGraphQL\Compiler\Util\ParameterReplacer.cs
	namespace 	
EntityGraphQL
 
. 
Compiler  
.  !
Util! %
{ 
internal 
class 
ParameterReplacer $
:% &
ExpressionVisitor' 8
{ 
private 

Expression 
newParam #
;# $
private 
Type 
toReplaceType "
;" #
private 
ParameterExpression #
	toReplace$ -
;- .
internal 

Expression 
Replace #
(# $

Expression$ .
node/ 3
,3 4
ParameterExpression5 H
	toReplaceI R
,R S

ExpressionT ^
newParam_ g
)g h
{ 	
this 
. 
newParam 
= 
newParam $
;$ %
this 
. 
	toReplace 
= 
	toReplace &
;& '
return 
Visit 
( 
node 
) 
; 
} 	
internal 

Expression 
ReplaceByType )
() *

Expression* 4
node5 9
,9 :
Type; ?
toReplaceType@ M
,M N

ExpressionO Y
newParamZ b
)b c
{ 	
this 
. 
newParam 
= 
newParam $
;$ %
this 
. 
toReplaceType 
=  
toReplaceType! .
;. /
return 
Visit 
( 
node 
) 
; 
} 	
	protected 
override 

Expression %
VisitParameter& 4
(4 5
ParameterExpression5 H
nodeI M
)M N
{   	
if!! 
(!! 
	toReplace!! 
!=!! 
null!! !
&&!!" $
	toReplace!!% .
==!!/ 1
node!!2 6
)!!6 7
return"" 
newParam"" 
;""  
if## 
(## 
toReplaceType## 
!=##  
null##! %
&&##& (
node##) -
.##- .
NodeType##. 6
==##7 9
ExpressionType##: H
.##H I
	Parameter##I R
&&##S U
toReplaceType##V c
==##d f
node##g k
.##k l
Type##l p
)##p q
return$$ 
newParam$$ 
;$$  
return%% 
node%% 
;%% 
}&& 	
	protected(( 
override(( 

Expression(( %
VisitLambda((& 1
<((1 2
T((2 3
>((3 4
(((4 5

Expression((5 ?
<((? @
T((@ A
>((A B
node((C G
)((G H
{)) 	
var** 
p** 
=** 
node** 
.** 

Parameters** #
.**# $
Select**$ *
(*** +
base**+ /
.**/ 0
Visit**0 5
)**5 6
.**6 7
Cast**7 ;
<**; <
ParameterExpression**< O
>**O P
(**P Q
)**Q R
;**R S
var++ 
body++ 
=++ 
base++ 
.++ 
Visit++ !
(++! "
node++" &
.++& '
Body++' +
)+++ ,
;++, -
return,, 

Expression,, 
.,, 
Lambda,, $
(,,$ %
body,,% )
,,,) *
p,,+ ,
),,, -
;,,- .
}-- 	
	protected// 
override// 

Expression// %
VisitMember//& 1
(//1 2
MemberExpression//2 B
node//C G
)//G H
{00 	
if11 
(11 
node11 
.11 

Expression11 
!=11  "
null11# '
&&11( *
node11+ /
.11/ 0

Expression110 :
.11: ;
NodeType11; C
==11D F
ExpressionType11G U
.11U V
	Parameter11V _
&&11` b
(11c d
node11d h
.11h i

Expression11i s
==11t v
	toReplace	11w �
||
11� �
node
11� �
.
11� �

Expression
11� �
.
11� �
Type
11� �
==
11� �
toReplaceType
11� �
)
11� �
)
11� �
{22 
var44 
newParam44 
=44 
base44 #
.44# $
Visit44$ )
(44) *
node44* .
.44. /

Expression44/ 9
)449 :
;44: ;
var55 
exp55 
=55 

Expression55 $
.55$ %
PropertyOrField55% 4
(554 5
newParam555 =
,55= >
node55? C
.55C D
Member55D J
.55J K
Name55K O
)55O P
;55P Q
return66 
exp66 
;66 
}77 
return88 
base88 
.88 
VisitMember88 #
(88# $
node88$ (
)88( )
;88) *
}99 	
}:: 
};; �-
CY:\Develop\EntityGraphQL\src\EntityGraphQL\EntityQueryExtensions.cs
	namespace 	
EntityGraphQL
 
{ 
public 

static 
class !
EntityQueryExtensions -
{ 
public 
static 
QueryResult !
QueryObject" -
<- .
TType. 3
>3 4
(4 5
this5 9
TType: ?
context@ G
,G H
QueryRequestI U
requestV ]
,] ^
ISchemaProvider_ n
schemaProvidero }
,} ~
params	 �
object
� �
[
� �
]
� �
mutationArgs
� �
)
� �
{ 	
return 
QueryObject 
( 
context &
,& '
request( /
,/ 0
schemaProvider1 ?
,? @
nullA E
,E F
falseG L
,L M
mutationArgsN Z
)Z [
;[ \
} 	
public(( 
static(( 
QueryResult(( !
QueryObject((" -
<((- .
TType((. 3
>((3 4
(((4 5
this((5 9
TType((: ?
context((@ G
,((G H
string((I O
query((P U
,((U V
ISchemaProvider((W f
schemaProvider((g u
,((u v
IMethodProvider	((w �
methodProvider
((� �
=
((� �
null
((� �
,
((� �
bool
((� �
includeDebugInfo
((� �
=
((� �
false
((� �
,
((� �
params
((� �
object
((� �
[
((� �
]
((� �
mutationArgs
((� �
)
((� �
{)) 	
return** 
QueryObject** 
(** 
context** &
,**& '
new**( +
QueryRequest**, 8
{**9 :
Query**; @
=**A B
query**C H
}**I J
,**J K
schemaProvider**L Z
,**Z [
methodProvider**\ j
,**j k
includeDebugInfo**l |
,**| }
mutationArgs	**~ �
)
**� �
;
**� �
}++ 	
public77 
static77 
QueryResult77 !
QueryObject77" -
<77- .
TType77. 3
>773 4
(774 5
this775 9
TType77: ?
context77@ G
,77G H
QueryRequest77I U
request77V ]
,77] ^
ISchemaProvider77_ n
schemaProvider77o }
,77} ~
IMethodProvider	77 �
methodProvider
77� �
=
77� �
null
77� �
,
77� �
bool
77� �
includeDebugInfo
77� �
=
77� �
false
77� �
,
77� �
params
77� �
object
77� �
[
77� �
]
77� �
mutationArgs
77� �
)
77� �
{88 	
if99 
(99 
methodProvider99 
==99 !
null99" &
)99& '
methodProvider:: 
=::  
new::! $!
DefaultMethodProvider::% :
(::: ;
)::; <
;::< =
	Stopwatch;; 
timer;; 
=;; 
null;; "
;;;" #
if<< 
(<< 
includeDebugInfo<<  
)<<  !
{== 
timer>> 
=>> 
new>> 
	Stopwatch>> %
(>>% &
)>>& '
;>>' (
timer?? 
.?? 
Start?? 
(?? 
)?? 
;?? 
}@@ 
QueryResultBB 
resultBB 
=BB  
nullBB! %
;BB% &
tryDD 
{EE 
varFF 
graphQLCompilerFF #
=FF$ %
newFF& )
GraphQLCompilerFF* 9
(FF9 :
schemaProviderFF: H
,FFH I
methodProviderFFJ X
)FFX Y
;FFY Z
varGG 
queryResultGG 
=GG  !
(GG" #
GraphQLResultNodeGG# 4
)GG4 5
graphQLCompilerGG5 D
.GGD E
CompileGGE L
(GGL M
requestGGM T
)GGT U
;GGU V
resultHH 
=HH 
queryResultHH $
.HH$ %
ExecuteQueryHH% 1
(HH1 2
contextHH2 9
,HH9 :
requestHH; B
.HHB C
OperationNameHHC P
,HHP Q
mutationArgsHHR ^
)HH^ _
;HH_ `
}II 
catchJJ 
(JJ 
	ExceptionJJ 
exJJ 
)JJ  
{KK 
resultMM 
=MM 
newMM 
QueryResultMM (
{MM) *
ErrorsMM* 0
=MM1 2
{MM3 4
newMM5 8
GraphQLErrorMM9 E
(MME F
exMMF H
.MMH I
InnerExceptionMMI W
!=MMX Z
nullMM[ _
?MM` a
exMMb d
.MMd e
InnerExceptionMMe s
.MMs t
MessageMMt {
:MM| }
ex	MM~ �
.
MM� �
Message
MM� �
)
MM� �
}
MM� �
}
MM� �
;
MM� �
}NN 
ifOO 
(OO 
includeDebugInfoOO  
&&OO! #
timerOO$ )
!=OO* ,
nullOO- 1
)OO1 2
{PP 
timerQQ 
.QQ 
StopQQ 
(QQ 
)QQ 
;QQ 
resultRR 
.RR 
SetDebugRR 
(RR  
newRR  #
{RR$ %
TotalMillisecondsRR& 7
=RR8 9
timerRR: ?
.RR? @
ElapsedMillisecondsRR@ S
}RRT U
)RRU V
;RRV W
}SS 
returnUU 
resultUU 
;UU 
}VV 	
}WW 
}XX �e
GY:\Develop\EntityGraphQL\src\EntityGraphQL\Extensions\LinqExtensions.cs
	namespace 	
EntityGraphQL
 
. 

Extensions "
{ 
public 

static 
class 
LinqExtensions &
{ 
public 
static 
IEnumerable !
<! "
TSource" )
>) *
Where+ 0
<0 1
TSource1 8
>8 9
(9 :
this: >
IEnumerable? J
<J K
TSourceK R
>R S
sourceT Z
,Z [
LambdaExpression\ l
	predicatem v
)v w
{ 	
var 
call 
= 

Expression !
.! "
Call" &
(& '
typeof' -
(- .

Enumerable. 8
)8 9
,9 :
$str; B
,B C
newD G
[G H
]H I
{J K
typeofL R
(R S
TSourceS Z
)Z [
}\ ]
,] ^

Expression_ i
.i j
Constantj r
(r s
sources y
)y z
,z {
	predicate	| �
)
� �
;
� �
return 
( 
IEnumerable 
<  
TSource  '
>' (
)( )

Expression) 3
.3 4
Lambda4 :
(: ;
call; ?
)? @
.@ A
CompileA H
(H I
)I J
.J K
DynamicInvokeK X
(X Y
)Y Z
;Z [
} 	
public 
static 
IEnumerable !
<! "
TSource" )
>) *
Any+ .
<. /
TSource/ 6
>6 7
(7 8
this8 <
IEnumerable= H
<H I
TSourceI P
>P Q
sourceR X
,X Y
LambdaExpressionZ j
	predicatek t
)t u
{ 	
var 
call 
= 

Expression !
.! "
Call" &
(& '
typeof' -
(- .

Enumerable. 8
)8 9
,9 :
$str; @
,@ A
newB E
[E F
]F G
{H I
typeofJ P
(P Q
TSourceQ X
)X Y
}Z [
,[ \

Expression] g
.g h
Constanth p
(p q
sourceq w
)w x
,x y
	predicate	z �
)
� �
;
� �
return 
( 
IEnumerable 
<  
TSource  '
>' (
)( )

Expression) 3
.3 4
Lambda4 :
(: ;
call; ?
)? @
.@ A
CompileA H
(H I
)I J
.J K
DynamicInvokeK X
(X Y
)Y Z
;Z [
} 	
public 
static 
IEnumerable !
<! "
TSource" )
>) *
Count+ 0
<0 1
TSource1 8
>8 9
(9 :
this: >
IEnumerable? J
<J K
TSourceK R
>R S
sourceT Z
,Z [
LambdaExpression\ l
	predicatem v
)v w
{ 	
var 
call 
= 

Expression !
.! "
Call" &
(& '
typeof' -
(- .

Enumerable. 8
)8 9
,9 :
$str; B
,B C
newD G
[G H
]H I
{J K
typeofL R
(R S
TSourceS Z
)Z [
}\ ]
,] ^

Expression_ i
.i j
Constantj r
(r s
sources y
)y z
,z {
	predicate	| �
)
� �
;
� �
return 
( 
IEnumerable 
<  
TSource  '
>' (
)( )

Expression) 3
.3 4
Lambda4 :
(: ;
call; ?
)? @
.@ A
CompileA H
(H I
)I J
.J K
DynamicInvokeK X
(X Y
)Y Z
;Z [
} 	
public 
static 
IEnumerable !
<! "
TSource" )
>) *
First+ 0
<0 1
TSource1 8
>8 9
(9 :
this: >
IEnumerable? J
<J K
TSourceK R
>R S
sourceT Z
,Z [
LambdaExpression\ l
	predicatem v
)v w
{ 	
var 
call 
= 

Expression !
.! "
Call" &
(& '
typeof' -
(- .

Enumerable. 8
)8 9
,9 :
$str; B
,B C
newD G
[G H
]H I
{J K
typeofL R
(R S
TSourceS Z
)Z [
}\ ]
,] ^

Expression_ i
.i j
Constantj r
(r s
sources y
)y z
,z {
	predicate	| �
)
� �
;
� �
return 
( 
IEnumerable 
<  
TSource  '
>' (
)( )

Expression) 3
.3 4
Lambda4 :
(: ;
call; ?
)? @
.@ A
CompileA H
(H I
)I J
.J K
DynamicInvokeK X
(X Y
)Y Z
;Z [
}   	
public!! 
static!! 
IEnumerable!! !
<!!! "
TSource!!" )
>!!) *
FirstOrDefault!!+ 9
<!!9 :
TSource!!: A
>!!A B
(!!B C
this!!C G
IEnumerable!!H S
<!!S T
TSource!!T [
>!![ \
source!!] c
,!!c d
LambdaExpression!!e u
	predicate!!v 
)	!! �
{"" 	
var## 
call## 
=## 

Expression## !
.##! "
Call##" &
(##& '
typeof##' -
(##- .

Enumerable##. 8
)##8 9
,##9 :
$str##; K
,##K L
new##M P
[##P Q
]##Q R
{##S T
typeof##U [
(##[ \
TSource##\ c
)##c d
}##e f
,##f g

Expression##h r
.##r s
Constant##s {
(##{ |
source	##| �
)
##� �
,
##� �
	predicate
##� �
)
##� �
;
##� �
return$$ 
($$ 
IEnumerable$$ 
<$$  
TSource$$  '
>$$' (
)$$( )

Expression$$) 3
.$$3 4
Lambda$$4 :
($$: ;
call$$; ?
)$$? @
.$$@ A
Compile$$A H
($$H I
)$$I J
.$$J K
DynamicInvoke$$K X
($$X Y
)$$Y Z
;$$Z [
}%% 	
public&& 
static&& 
IEnumerable&& !
<&&! "
TSource&&" )
>&&) *
Last&&+ /
<&&/ 0
TSource&&0 7
>&&7 8
(&&8 9
this&&9 =
IEnumerable&&> I
<&&I J
TSource&&J Q
>&&Q R
source&&S Y
,&&Y Z
LambdaExpression&&[ k
	predicate&&l u
)&&u v
{'' 	
var(( 
call(( 
=(( 

Expression(( !
.((! "
Call((" &
(((& '
typeof((' -
(((- .

Enumerable((. 8
)((8 9
,((9 :
$str((; A
,((A B
new((C F
[((F G
]((G H
{((I J
typeof((K Q
(((Q R
TSource((R Y
)((Y Z
}(([ \
,((\ ]

Expression((^ h
.((h i
Constant((i q
(((q r
source((r x
)((x y
,((y z
	predicate	(({ �
)
((� �
;
((� �
return)) 
()) 
IEnumerable)) 
<))  
TSource))  '
>))' (
)))( )

Expression))) 3
.))3 4
Lambda))4 :
()): ;
call)); ?
)))? @
.))@ A
Compile))A H
())H I
)))I J
.))J K
DynamicInvoke))K X
())X Y
)))Y Z
;))Z [
}** 	
public++ 
static++ 
IEnumerable++ !
<++! "
TSource++" )
>++) *
LastOrDefault+++ 8
<++8 9
TSource++9 @
>++@ A
(++A B
this++B F
IEnumerable++G R
<++R S
TSource++S Z
>++Z [
source++\ b
,++b c
LambdaExpression++d t
	predicate++u ~
)++~ 
{,, 	
var-- 
call-- 
=-- 

Expression-- !
.--! "
Call--" &
(--& '
typeof--' -
(--- .

Enumerable--. 8
)--8 9
,--9 :
$str--; J
,--J K
new--L O
[--O P
]--P Q
{--R S
typeof--T Z
(--Z [
TSource--[ b
)--b c
}--d e
,--e f

Expression--g q
.--q r
Constant--r z
(--z {
source	--{ �
)
--� �
,
--� �
	predicate
--� �
)
--� �
;
--� �
return.. 
(.. 
IEnumerable.. 
<..  
TSource..  '
>..' (
)..( )

Expression..) 3
...3 4
Lambda..4 :
(..: ;
call..; ?
)..? @
...@ A
Compile..A H
(..H I
)..I J
...J K
DynamicInvoke..K X
(..X Y
)..Y Z
;..Z [
}// 	
public11 
static11 

IQueryable11  
<11  !
TSource11! (
>11( )
Take11* .
<11. /
TSource11/ 6
>116 7
(117 8
this118 <

IQueryable11= G
<11G H
TSource11H O
>11O P
source11Q W
,11W X
int11Y \
?11\ ]
count11^ c
)11c d
{22 	
if33 
(33 
!33 
count33 
.33 
HasValue33 
)33  
return44 
source44 
;44 
return66 
	Queryable66 
.66 
Take66 !
(66! "
source66" (
,66( )
count66* /
.66/ 0
Value660 5
)665 6
;666 7
}77 	
publicAA 
staticAA 

IQueryableAA  
<AA  !
TSourceAA! (
>AA( )
	WhereWhenAA* 3
<AA3 4
TSourceAA4 ;
>AA; <
(AA< =
thisAA= A

IQueryableAAB L
<AAL M
TSourceAAM T
>AAT U
sourceAAV \
,AA\ ]

ExpressionAA^ h
<AAh i
FuncAAi m
<AAm n
TSourceAAn u
,AAu v
boolAAw {
>AA{ |
>AA| }
wherePredicate	AA~ �
,
AA� �
bool
AA� �
applyPredicate
AA� �
)
AA� �
{BB 	
ifCC 
(CC 
applyPredicateCC 
)CC 
returnDD 
	QueryableDD  
.DD  !
WhereDD! &
(DD& '
sourceDD' -
,DD- .
wherePredicateDD/ =
)DD= >
;DD> ?
returnFF 
sourceFF 
;FF 
}GG 	
publicQQ 
staticQQ 

IQueryableQQ  
<QQ  !
TSourceQQ! (
>QQ( )
	WhereWhenQQ* 3
<QQ3 4
TSourceQQ4 ;
>QQ; <
(QQ< =
thisQQ= A
IEnumerableQQB M
<QQM N
TSourceQQN U
>QQU V
sourceQQW ]
,QQ] ^

ExpressionQQ_ i
<QQi j
FuncQQj n
<QQn o
TSourceQQo v
,QQv w
boolQQx |
>QQ| }
>QQ} ~
wherePredicate	QQ �
,
QQ� �
bool
QQ� �
applyPredicate
QQ� �
)
QQ� �
{RR 	
ifSS 
(SS 
applyPredicateSS 
)SS 
returnTT 
	QueryableTT  
.TT  !
WhereTT! &
(TT& '
sourceTT' -
.TT- .
AsQueryableTT. 9
(TT9 :
)TT: ;
,TT; <
wherePredicateTT= K
)TTK L
;TTL M
returnVV 
sourceVV 
.VV 
AsQueryableVV %
(VV% &
)VV& '
;VV' (
}WW 	
}XX 
}YY �/
GY:\Develop\EntityGraphQL\src\EntityGraphQL\Extensions\TypeExtensions.cs
	namespace 	
EntityGraphQL
 
. 

Extensions "
{ 
public 

static 
class 
TypeExtensions &
{		 
public 
static 
bool 
IsEnumerableOrArray .
(. /
this/ 3
Type4 8
source9 ?
)? @
{ 	
if 
( 
source 
== 
typeof  
(  !
string! '
)' (
||) +
source, 2
==3 5
typeof6 <
(< =
byte= A
[A B
]B C
)C D
)D E
return 
false 
; 
if 
( 
source 
. 
GetTypeInfo "
(" #
)# $
.$ %
IsArray% ,
), -
{ 
return 
true 
; 
} 
var 
isEnumerable 
= 
false $
;$ %
if 
( 
source 
. 
GetTypeInfo "
(" #
)# $
.$ %
IsGenericType% 2
)2 3
{ 
isEnumerable 
= #
IsGenericTypeEnumerable 6
(6 7
source7 =
)= >
;> ?
} 
return 
isEnumerable 
;  
} 	
private   
static   
bool   #
IsGenericTypeEnumerable   3
(  3 4
Type  4 8
source  9 ?
)  ? @
{!! 	
bool"" 
isEnumerable"" 
="" 
(""  !
source""! '
.""' (
GetTypeInfo""( 3
(""3 4
)""4 5
.""5 6
IsGenericType""6 C
&&""D F
source""G M
.""M N$
GetGenericTypeDefinition""N f
(""f g
)""g h
==""i k
typeof""l r
(""r s
IEnumerable""s ~
<""~ 
>	"" �
)
""� �
||
""� �
source
""� �
.
""� �
GetTypeInfo
""� �
(
""� �
)
""� �
.
""� �
IsGenericType
""� �
&&
""� �
source
""� �
.
""� �&
GetGenericTypeDefinition
""� �
(
""� �
)
""� �
==
""� �
typeof
""� �
(
""� �

IQueryable
""� �
<
""� �
>
""� �
)
""� �
)
""� �
;
""� �
if## 
(## 
!## 
isEnumerable## 
)## 
{$$ 
foreach%% 
(%% 
var%% 
intType%% $
in%%% '
source%%( .
.%%. /
GetInterfaces%%/ <
(%%< =
)%%= >
)%%> ?
{&& 
isEnumerable''  
=''! "#
IsGenericTypeEnumerable''# :
('': ;
intType''; B
)''B C
;''C D
if(( 
((( 
isEnumerable(( $
)(($ %
break)) 
;)) 
}** 
}++ 
return-- 
isEnumerable-- 
;--  
}.. 	
public55 
static55 
Type55 $
GetEnumerableOrArrayType55 3
(553 4
this554 8
Type559 =
type55> B
)55B C
{66 	
if77 
(77 
type77 
.77 
IsArray77 
)77 
{88 
return99 
type99 
.99 
GetElementType99 *
(99* +
)99+ ,
;99, -
}:: 
if;; 
(;; 
type;; 
.;; 
GetTypeInfo;;  
(;;  !
);;! "
.;;" #
IsGenericType;;# 0
&&;;1 3
type;;4 8
.;;8 9$
GetGenericTypeDefinition;;9 Q
(;;Q R
);;R S
==;;T V
typeof;;W ]
(;;] ^
IEnumerable;;^ i
<;;i j
>;;j k
);;k l
);;l m
return<< 
type<< 
.<< 
GetGenericArguments<< /
(<</ 0
)<<0 1
[<<1 2
$num<<2 3
]<<3 4
;<<4 5
foreach== 
(== 
var== 
intType==  
in==! #
type==$ (
.==( )
GetInterfaces==) 6
(==6 7
)==7 8
)==8 9
{>> 
if?? 
(?? 
intType?? 
.?? 
IsEnumerableOrArray?? /
(??/ 0
)??0 1
)??1 2
{@@ 
returnAA 
intTypeAA "
.AA" #
GetGenericArgumentsAA# 6
(AA6 7
)AA7 8
[AA8 9
$numAA9 :
]AA: ;
;AA; <
}BB 
varCC 
deepIntTypeCC 
=CC  !
intTypeCC" )
.CC) *$
GetEnumerableOrArrayTypeCC* B
(CCB C
)CCC D
;CCD E
ifDD 
(DD 
deepIntTypeDD 
!=DD  "
nullDD# '
)DD' (
returnEE 
deepIntTypeEE &
.EE& '
GetGenericArgumentsEE' :
(EE: ;
)EE; <
[EE< =
$numEE= >
]EE> ?
;EE? @
}FF 
returnGG 
nullGG 
;GG 
}HH 	
publicJJ 
staticJJ 
boolJJ 
IsNullableTypeJJ )
(JJ) *
thisJJ* .
TypeJJ/ 3
tJJ4 5
)JJ5 6
{KK 	
returnLL 
tLL 
.LL 
GetTypeInfoLL  
(LL  !
)LL! "
.LL" #
IsGenericTypeLL# 0
&&LL1 3
tLL4 5
.LL5 6$
GetGenericTypeDefinitionLL6 N
(LLN O
)LLO P
==LLQ S
typeofLLT Z
(LLZ [
NullableLL[ c
<LLc d
>LLd e
)LLe f
;LLf g
}MM 	
}NN 
}OO �
MY:\Develop\EntityGraphQL\src\EntityGraphQL\LinqQuery\DefaultMethodProvider.cs
	namespace

 	
EntityGraphQL


 
.

 
	LinqQuery

 !
{ 
public%% 

class%% !
DefaultMethodProvider%% &
:%%' (
IMethodProvider%%) 8
{&& 
private(( 

Dictionary(( 
<(( 
string(( !
,((! "
Func((# '
<((' (

Expression((( 2
,((2 3

Expression((4 >
,((> ?
string((@ F
,((F G
ExpressionResult((H X
[((X Y
]((Y Z
,((Z [
ExpressionResult((\ l
>((l m
>((m n
_supportedMethods	((o �
=
((� �
new
((� �

Dictionary
((� �
<
((� �
string
((� �
,
((� �
Func
((� �
<
((� �

Expression
((� �
,
((� �

Expression
((� �
,
((� �
string
((� �
,
((� �
ExpressionResult
((� �
[
((� �
]
((� �
,
((� �
ExpressionResult
((� �
>
((� �
>
((� �
(
((� �
StringComparer
((� �
.
((� �
OrdinalIgnoreCase
((� �
)
((� �
{)) 	
{** 
$str** 
,** 
MakeWhereMethod** &
}**' (
,**( )
{++ 
$str++ 
,++ 
MakeWhereMethod++ '
}++( )
,++) *
{,, 
$str,, 
,,, 
MakeFirstMethod,, &
},,' (
,,,( )
{-- 
$str-- 
,-- 
MakeLastMethod-- $
}--% &
,--& '
{.. 
$str.. 
,.. 
MakeTakeMethod.. $
}..% &
,..& '
{// 
$str// 
,// 
MakeSkipMethod// $
}//% &
,//& '
{00 
$str00 
,00 
MakeCountMethod00 &
}00' (
,00( )
{11 
$str11 
,11 
MakeOrderByMethod11 *
}11+ ,
,11, -
{22 
$str22 
,22 !
MakeOrderByDescMethod22 2
}223 4
,224 5
}33 	
;33	 

public55 
bool55 
EntityTypeHasMethod55 '
(55' (
Type55( ,
context55- 4
,554 5
string556 <

methodName55= G
)55G H
{66 	
return77 
_supportedMethods77 $
.77$ %
ContainsKey77% 0
(770 1

methodName771 ;
)77; <
;77< =
}88 	
public:: 
ExpressionResult:: 
GetMethodContext::  0
(::0 1
ExpressionResult::1 A
context::B I
,::I J
string::K Q

methodName::R \
)::\ ]
{;; 	
return?? $
GetContextFromEnumerable?? +
(??+ ,
context??, 3
)??3 4
;??4 5
}@@ 	
publicBB 
ExpressionResultBB 
MakeCallBB  (
(BB( )

ExpressionBB) 3
contextBB4 ;
,BB; <

ExpressionBB= G

argContextBBH R
,BBR S
stringBBT Z

methodNameBB[ e
,BBe f
IEnumerableBBg r
<BBr s
ExpressionResult	BBs �
>
BB� �
args
BB� �
)
BB� �
{CC 	
ifDD 
(DD 
_supportedMethodsDD !
.DD! "
ContainsKeyDD" -
(DD- .

methodNameDD. 8
)DD8 9
)DD9 :
{EE 
returnFF 
_supportedMethodsFF (
[FF( )

methodNameFF) 3
]FF3 4
(FF4 5
contextFF5 <
,FF< =

argContextFF> H
,FFH I

methodNameFFJ T
,FFT U
argsFFV Z
!=FF[ ]
nullFF^ b
?FFc d
argsFFe i
.FFi j
ToArrayFFj q
(FFq r
)FFr s
:FFt u
newFFv y
ExpressionResult	FFz �
[
FF� �
]
FF� �
{
FF� �
}
FF� �
)
FF� �
;
FF� �
}GG 
throwHH 
newHH *
EntityGraphQLCompilerExceptionHH 4
(HH4 5
$"HH5 7
Unsupported method HH7 J
{HHJ K

methodNameHHK U
}HHU V
"HHV W
)HHW X
;HHX Y
}II 	
privateKK 
staticKK 
ExpressionResultKK '
MakeWhereMethodKK( 7
(KK7 8

ExpressionKK8 B
contextKKC J
,KKJ K

ExpressionKKL V

argContextKKW a
,KKa b
stringKKc i

methodNameKKj t
,KKt u
ExpressionResult	KKv �
[
KK� �
]
KK� �
args
KK� �
)
KK� �
{LL 	
ExpectArgsCountMM 
(MM 
$numMM 
,MM 
argsMM #
,MM# $

methodNameMM% /
)MM/ 0
;MM0 1
varNN 
	predicateNN 
=NN 
argsNN  
.NN  !
FirstNN! &
(NN& '
)NN' (
;NN( )
	predicateOO 
=OO 
ConvertTypeIfWeCanOO *
(OO* +

methodNameOO+ 5
,OO5 6
	predicateOO7 @
,OO@ A
typeofOOB H
(OOH I
boolOOI M
)OOM N
)OON O
;OOO P
varPP 
lambdaPP 
=PP 

ExpressionPP #
.PP# $
LambdaPP$ *
(PP* +
	predicatePP+ 4
,PP4 5

argContextPP6 @
asPPA C
ParameterExpressionPPD W
)PPW X
;PPX Y
returnQQ 
ExpressionUtilQQ !
.QQ! "
MakeExpressionCallQQ" 4
(QQ4 5
newQQ5 8
[QQ8 9
]QQ9 :
{QQ; <
typeofQQ= C
(QQC D
	QueryableQQD M
)QQM N
,QQN O
typeofQQP V
(QQV W

EnumerableQQW a
)QQa b
}QQc d
,QQd e
$strQQf m
,QQm n
newQQo r
TypeQQs w
[QQw x
]QQx y
{QQz {

argContext	QQ| �
.
QQ� �
Type
QQ� �
}
QQ� �
,
QQ� �
context
QQ� �
,
QQ� �
lambda
QQ� �
)
QQ� �
;
QQ� �
}RR 	
privateTT 
staticTT 
ExpressionResultTT '
MakeFirstMethodTT( 7
(TT7 8

ExpressionTT8 B
contextTTC J
,TTJ K

ExpressionTTL V

argContextTTW a
,TTa b
stringTTc i

methodNameTTj t
,TTt u
ExpressionResult	TTv �
[
TT� �
]
TT� �
args
TT� �
)
TT� �
{UU 	
returnVV *
MakeOptionalFilterArgumentCallVV 1
(VV1 2
contextVV2 9
,VV9 :

argContextVV; E
,VVE F

methodNameVVG Q
,VVQ R
argsVVS W
,VVW X
$strVVY `
)VV` a
;VVa b
}WW 	
privateYY 
staticYY 
ExpressionResultYY '
MakeCountMethodYY( 7
(YY7 8

ExpressionYY8 B
contextYYC J
,YYJ K

ExpressionYYL V

argContextYYW a
,YYa b
stringYYc i

methodNameYYj t
,YYt u
ExpressionResult	YYv �
[
YY� �
]
YY� �
args
YY� �
)
YY� �
{ZZ 	
return[[ *
MakeOptionalFilterArgumentCall[[ 1
([[1 2
context[[2 9
,[[9 :

argContext[[; E
,[[E F

methodName[[G Q
,[[Q R
args[[S W
,[[W X
$str[[Y `
)[[` a
;[[a b
}\\ 	
private^^ 
static^^ 
ExpressionResult^^ '*
MakeOptionalFilterArgumentCall^^( F
(^^F G

Expression^^G Q
context^^R Y
,^^Y Z

Expression^^[ e

argContext^^f p
,^^p q
string^^r x

methodName	^^y �
,
^^� �
ExpressionResult
^^� �
[
^^� �
]
^^� �
args
^^� �
,
^^� �
string
^^� �
actualMethodName
^^� �
)
^^� �
{__ 	"
ExpectArgsCountBetween`` "
(``" #
$num``# $
,``$ %
$num``& '
,``' (
args``) -
,``- .

methodName``/ 9
)``9 :
;``: ;
varbb 
allArgsbb 
=bb 
newbb 
Listbb "
<bb" #

Expressionbb# -
>bb- .
{bb/ 0
contextbb1 8
}bb9 :
;bb: ;
ifcc 
(cc 
argscc 
.cc 
Countcc 
(cc 
)cc 
==cc 
$numcc  !
)cc! "
{dd 
varee 
	predicateee 
=ee 
argsee  $
.ee$ %
Firstee% *
(ee* +
)ee+ ,
;ee, -
	predicateff 
=ff 
ConvertTypeIfWeCanff .
(ff. /

methodNameff/ 9
,ff9 :
	predicateff; D
,ffD E
typeofffF L
(ffL M
boolffM Q
)ffQ R
)ffR S
;ffS T
allArgsgg 
.gg 
Addgg 
(gg 

Expressiongg &
.gg& '
Lambdagg' -
(gg- .
	predicategg. 7
,gg7 8

argContextgg9 C
asggD F
ParameterExpressionggG Z
)ggZ [
)gg[ \
;gg\ ]
}hh 
returnjj 
ExpressionUtiljj !
.jj! "
MakeExpressionCalljj" 4
(jj4 5
newjj5 8
[jj8 9
]jj9 :
{jj; <
typeofjj= C
(jjC D
	QueryablejjD M
)jjM N
,jjN O
typeofjjP V
(jjV W

EnumerablejjW a
)jja b
}jjc d
,jjd e
actualMethodNamejjf v
,jjv w
newjjx {
Type	jj| �
[
jj� �
]
jj� �
{
jj� �

argContext
jj� �
.
jj� �
Type
jj� �
}
jj� �
,
jj� �
allArgs
jj� �
.
jj� �
ToArray
jj� �
(
jj� �
)
jj� �
)
jj� �
;
jj� �
}kk 	
privatemm 
staticmm 
ExpressionResultmm '
MakeLastMethodmm( 6
(mm6 7

Expressionmm7 A
contextmmB I
,mmI J

ExpressionmmK U

argContextmmV `
,mm` a
stringmmb h

methodNamemmi s
,mms t
ExpressionResult	mmu �
[
mm� �
]
mm� �
args
mm� �
)
mm� �
{nn 	
returnoo *
MakeOptionalFilterArgumentCalloo 1
(oo1 2
contextoo2 9
,oo9 :

argContextoo; E
,ooE F

methodNameooG Q
,ooQ R
argsooS W
,ooW X
$strooY _
)oo_ `
;oo` a
}pp 	
privaterr 
staticrr 
ExpressionResultrr '
MakeTakeMethodrr( 6
(rr6 7

Expressionrr7 A
contextrrB I
,rrI J

ExpressionrrK U

argContextrrV `
,rr` a
stringrrb h

methodNamerri s
,rrs t
ExpressionResult	rru �
[
rr� �
]
rr� �
args
rr� �
)
rr� �
{ss 	
ExpectArgsCounttt 
(tt 
$numtt 
,tt 
argstt #
,tt# $

methodNamett% /
)tt/ 0
;tt0 1
varuu 
amountuu 
=uu 
argsuu 
.uu 
Firstuu #
(uu# $
)uu$ %
;uu% &
amountvv 
=vv 
ConvertTypeIfWeCanvv '
(vv' (

methodNamevv( 2
,vv2 3
amountvv4 :
,vv: ;
typeofvv< B
(vvB C
intvvC F
)vvF G
)vvG H
;vvH I
returnxx 
ExpressionUtilxx !
.xx! "
MakeExpressionCallxx" 4
(xx4 5
newxx5 8
[xx8 9
]xx9 :
{xx; <
typeofxx= C
(xxC D
	QueryablexxD M
)xxM N
,xxN O
typeofxxP V
(xxV W

EnumerablexxW a
)xxa b
}xxc d
,xxd e
$strxxf l
,xxl m
newxxn q
Typexxr v
[xxv w
]xxw x
{xxy z

argContext	xx{ �
.
xx� �
Type
xx� �
}
xx� �
,
xx� �
context
xx� �
,
xx� �
amount
xx� �
)
xx� �
;
xx� �
}yy 	
private{{ 
static{{ 
ExpressionResult{{ '
MakeSkipMethod{{( 6
({{6 7

Expression{{7 A
context{{B I
,{{I J

Expression{{K U

argContext{{V `
,{{` a
string{{b h

methodName{{i s
,{{s t
ExpressionResult	{{u �
[
{{� �
]
{{� �
args
{{� �
)
{{� �
{|| 	
ExpectArgsCount}} 
(}} 
$num}} 
,}} 
args}} #
,}}# $

methodName}}% /
)}}/ 0
;}}0 1
var~~ 
amount~~ 
=~~ 
args~~ 
.~~ 
First~~ #
(~~# $
)~~$ %
;~~% &
amount 
= 
ConvertTypeIfWeCan '
(' (

methodName( 2
,2 3
amount4 :
,: ;
typeof< B
(B C
intC F
)F G
)G H
;H I
return
�� 
ExpressionUtil
�� !
.
��! " 
MakeExpressionCall
��" 4
(
��4 5
new
��5 8
[
��8 9
]
��9 :
{
��; <
typeof
��= C
(
��C D
	Queryable
��D M
)
��M N
,
��N O
typeof
��P V
(
��V W

Enumerable
��W a
)
��a b
}
��c d
,
��d e
$str
��f l
,
��l m
new
��n q
Type
��r v
[
��v w
]
��w x
{
��y z

argContext��{ �
.��� �
Type��� �
}��� �
,��� �
context��� �
,��� �
amount��� �
)��� �
;��� �
}
�� 	
private
�� 
static
�� 
ExpressionResult
�� '
MakeOrderByMethod
��( 9
(
��9 :

Expression
��: D
context
��E L
,
��L M

Expression
��N X

argContext
��Y c
,
��c d
string
��e k

methodName
��l v
,
��v w
ExpressionResult��x �
[��� �
]��� �
args��� �
)��� �
{
�� 	
ExpectArgsCount
�� 
(
�� 
$num
�� 
,
�� 
args
�� #
,
��# $

methodName
��% /
)
��/ 0
;
��0 1
var
�� 
column
�� 
=
�� 
args
�� 
.
�� 
First
�� #
(
��# $
)
��$ %
;
��% &
var
�� 
lambda
�� 
=
�� 

Expression
�� #
.
��# $
Lambda
��$ *
(
��* +
column
��+ 1
,
��1 2

argContext
��3 =
as
��> @!
ParameterExpression
��A T
)
��T U
;
��U V
return
�� 
ExpressionUtil
�� !
.
��! " 
MakeExpressionCall
��" 4
(
��4 5
new
��5 8
[
��8 9
]
��9 :
{
��; <
typeof
��= C
(
��C D
	Queryable
��D M
)
��M N
,
��N O
typeof
��P V
(
��V W

Enumerable
��W a
)
��a b
}
��c d
,
��d e
$str
��f o
,
��o p
new
��q t
Type
��u y
[
��y z
]
��z {
{
��| }

argContext��~ �
.��� �
Type��� �
,��� �
column��� �
.��� �
Type��� �
}��� �
,��� �
context��� �
,��� �
lambda��� �
)��� �
;��� �
}
�� 	
private
�� 
static
�� 
ExpressionResult
�� '#
MakeOrderByDescMethod
��( =
(
��= >

Expression
��> H
context
��I P
,
��P Q

Expression
��R \

argContext
��] g
,
��g h
string
��i o

methodName
��p z
,
��z {
ExpressionResult��| �
[��� �
]��� �
args��� �
)��� �
{
�� 	
ExpectArgsCount
�� 
(
�� 
$num
�� 
,
�� 
args
�� #
,
��# $

methodName
��% /
)
��/ 0
;
��0 1
var
�� 
column
�� 
=
�� 
args
�� 
.
�� 
First
�� #
(
��# $
)
��$ %
;
��% &
var
�� 
lambda
�� 
=
�� 

Expression
�� #
.
��# $
Lambda
��$ *
(
��* +
column
��+ 1
,
��1 2

argContext
��3 =
as
��> @!
ParameterExpression
��A T
)
��T U
;
��U V
return
�� 
ExpressionUtil
�� !
.
��! " 
MakeExpressionCall
��" 4
(
��4 5
new
��5 8
[
��8 9
]
��9 :
{
��; <
typeof
��= C
(
��C D
	Queryable
��D M
)
��M N
,
��N O
typeof
��P V
(
��V W

Enumerable
��W a
)
��a b
}
��c d
,
��d e
$str
��f y
,
��y z
new
��{ ~
Type�� �
[��� �
]��� �
{��� �

argContext��� �
.��� �
Type��� �
,��� �
column��� �
.��� �
Type��� �
}��� �
,��� �
context��� �
,��� �
lambda��� �
)��� �
;��� �
}
�� 	
private
�� 
static
�� 
ExpressionResult
�� '&
GetContextFromEnumerable
��( @
(
��@ A
ExpressionResult
��A Q
context
��R Y
)
��Y Z
{
�� 	
if
�� 
(
�� 
context
�� 
.
�� 
Type
�� 
.
�� !
IsEnumerableOrArray
�� 0
(
��0 1
)
��1 2
)
��2 3
{
�� 
return
�� 
(
�� 
ExpressionResult
�� (
)
��( )

Expression
��) 3
.
��3 4
	Parameter
��4 =
(
��= >
context
��> E
.
��E F
Type
��F J
.
��J K!
GetGenericArguments
��K ^
(
��^ _
)
��_ `
[
��` a
$num
��a b
]
��b c
)
��c d
;
��d e
}
�� 
var
�� 
t
�� 
=
�� 
context
�� 
.
�� 
Type
��  
.
��  !&
GetEnumerableOrArrayType
��! 9
(
��9 :
)
��: ;
;
��; <
if
�� 
(
�� 
t
�� 
!=
�� 
null
�� 
)
�� 
return
�� 
(
�� 
ExpressionResult
�� (
)
��( )

Expression
��) 3
.
��3 4
	Parameter
��4 =
(
��= >
t
��> ?
)
��? @
;
��@ A
return
�� 
context
�� 
;
�� 
}
�� 	
private
�� 
static
�� 
void
�� 
ExpectArgsCount
�� +
(
��+ ,
int
��, /
count
��0 5
,
��5 6
ExpressionResult
��7 G
[
��G H
]
��H I
args
��J N
,
��N O
string
��P V
method
��W ]
)
��] ^
{
�� 	
if
�� 
(
�� 
args
�� 
.
�� 
Count
�� 
(
�� 
)
�� 
!=
�� 
count
��  %
)
��% &
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 8
(
��8 9
$"
��9 ;
Method '
��; C
{
��C D
method
��D J
}
��J K

' expects 
��K U
{
��U V
count
��V [
}
��[ \
 argument(s) but 
��\ m
{
��m n
args
��n r
.
��r s
Count
��s x
(
��x y
)
��y z
}
��z {
 were supplied��{ �
"��� �
)��� �
;��� �
}
�� 	
private
�� 
static
�� 
void
�� $
ExpectArgsCountBetween
�� 2
(
��2 3
int
��3 6
low
��7 :
,
��: ;
int
��< ?
high
��@ D
,
��D E
ExpressionResult
��F V
[
��V W
]
��W X
args
��Y ]
,
��] ^
string
��_ e
method
��f l
)
��l m
{
�� 	
if
�� 
(
�� 
args
�� 
.
�� 
Count
�� 
(
�� 
)
�� 
<
�� 
low
�� "
||
��# %
args
��& *
.
��* +
Count
��+ 0
(
��0 1
)
��1 2
>
��3 4
high
��5 9
)
��9 :
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 8
(
��8 9
$"
��9 ;
Method '
��; C
{
��C D
method
��D J
}
��J K

' expects 
��K U
{
��U V
low
��V Y
}
��Y Z
-
��Z [
{
��[ \
high
��\ `
}
��` a
 argument(s) but 
��a r
{
��r s
args
��s w
.
��w x
Count
��x }
(
��} ~
)
��~ 
}�� �
 were supplied��� �
"��� �
)��� �
;��� �
}
�� 	
private
�� 
static
�� 
ExpressionResult
�� ' 
ConvertTypeIfWeCan
��( :
(
��: ;
string
��; A

methodName
��B L
,
��L M
ExpressionResult
��N ^
argExp
��_ e
,
��e f
Type
��g k
expected
��l t
)
��t u
{
�� 	
if
�� 
(
�� 
expected
�� 
!=
�� 
argExp
�� "
.
��" #
Type
��# '
)
��' (
{
�� 
try
�� 
{
�� 
return
�� 
(
�� 
ExpressionResult
�� ,
)
��, -

Expression
��- 7
.
��7 8
Convert
��8 ?
(
��? @
argExp
��@ F
,
��F G
expected
��H P
)
��P Q
;
��Q R
}
�� 
catch
�� 
(
�� 
	Exception
��  
)
��  !
{
�� 
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� <
(
��< =
$"
��= ?
Method '
��? G
{
��G H

methodName
��H R
}
��R S7
)' expects parameter that evaluates to a '
��S |
{
��| }
expected��} �
}��� �0
 ' result but found result type '��� �
{��� �
argExp��� �
.��� �
Type��� �
}��� �
'��� �
"��� �
)��� �
;��� �
}
�� 
}
�� 
return
�� 
argExp
�� 
;
�� 
}
�� 	
}
�� 
}�� �
GY:\Develop\EntityGraphQL\src\EntityGraphQL\LinqQuery\IMethodProvider.cs
	namespace 	
EntityGraphQL
 
. 
	LinqQuery !
{ 
public 

	interface 
IMethodProvider $
{		 
bool

 
EntityTypeHasMethod

  
(

  !
Type

! %
context

& -
,

- .
string

/ 5

methodName

6 @
)

@ A
;

A B
ExpressionResult 
GetMethodContext )
() *
ExpressionResult* :
context; B
,B C
stringD J

methodNameK U
)U V
;V W
ExpressionResult 
MakeCall !
(! "

Expression" ,
context- 4
,4 5

Expression6 @

argContextA K
,K L
stringM S

methodNameT ^
,^ _
IEnumerable` k
<k l
ExpressionResultl |
>| }
args	~ �
)
� �
;
� �
} 
} �
<Y:\Develop\EntityGraphQL\src\EntityGraphQL\QueryException.cs
	namespace 	
EntityGraphQL
 
{ 
[ 
System 
. 
Serializable 
] 
public 

class 
QueryException 
:  !
System" (
.( )
	Exception) 2
{ 
public 
QueryException 
( 
) 
{  !
}" #
public 
QueryException 
( 
string $
message% ,
), -
:. /
base0 4
(4 5
message5 <
)< =
{> ?
}@ A
public 
QueryException 
( 
string $
message% ,
,, -
System. 4
.4 5
	Exception5 >
inner? D
)D E
:F G
baseH L
(L M
messageM T
,T U
innerV [
)[ \
{] ^
}_ `
}		 
}

 �
:Y:\Develop\EntityGraphQL\src\EntityGraphQL\QueryRequest.cs
	namespace 	
EntityGraphQL
 
{ 
public 

class 
QueryRequest 
{		 
public 
string 
OperationName #
{$ %
get& )
;) *
set+ .
;. /
}0 1
public 
string 
Query 
{ 
get !
;! "
set# &
;& '
}( )
public 
QueryVariables 
	Variables '
{( )
get* -
;- .
set/ 2
;2 3
}4 5
} 
public 

class 
QueryVariables 
:  !

Dictionary" ,
<, -
string- 3
,3 4
object5 ;
>; <
{ 
public 
object 
GetValueFor !
(! "
string" (
varKey) /
)/ 0
{ 	
return 
ContainsKey 
( 
varKey %
)% &
?' (
this) -
[- .
varKey. 4
]4 5
:6 7
null8 <
;< =
} 	
} 
public$$ 

class$$ 
GraphQLError$$ 
{%% 
private&& 
string&& 
message&& 
;&& 
public(( 
GraphQLError(( 
((( 
string(( "
message((# *
)((* +
{)) 	
this** 
.** 
Message** 
=** 
message** "
;**" #
}++ 	
public-- 
string-- 
Message-- 
{-- 
get--  #
=>--$ &
message--' .
;--. /
set--0 3
=>--4 6
message--7 >
=--? @
value--A F
;--F G
}--H I
}.. 
}// �
9Y:\Develop\EntityGraphQL\src\EntityGraphQL\QueryResult.cs
	namespace 	
EntityGraphQL
 
{ 
public 

class 
QueryResult 
{		 
[

 	
JsonProperty

	 
(

 
$str

 
)

 
]

  
public 
List 
< 
GraphQLError  
>  !
Errors" (
=>) +
(, -
List- 1
<1 2
GraphQLError2 >
>> ?
)? @
dataResults@ K
[K L
$strL T
]T U
;U V
[ 	
JsonProperty	 
( 
$str 
) 
] 
public  
ConcurrentDictionary #
<# $
string$ *
,* +
object, 2
>2 3
Data4 8
=>9 ;
(< = 
ConcurrentDictionary= Q
<Q R
stringR X
,X Y
objectZ `
>` a
)a b
dataResultsb m
[m n
$strn t
]t u
;u v
private 
readonly  
ConcurrentDictionary -
<- .
string. 4
,4 5
object6 <
>< =
dataResults> I
=J K
newL O 
ConcurrentDictionaryP d
<d e
stringe k
,k l
objectm s
>s t
(t u
)u v
;v w
public 
QueryResult 
( 
) 
{ 	
dataResults 
[ 
$str  
]  !
=" #
new$ '
List( ,
<, -
GraphQLError- 9
>9 :
(: ;
); <
;< =
dataResults 
[ 
$str 
] 
=  !
new" % 
ConcurrentDictionary& :
<: ;
string; A
,A B
objectC I
>I J
(J K
)K L
;L M
} 	
internal 
void 
SetDebug 
( 
object %
	debugData& /
)/ 0
{ 	
dataResults 
[ 
$str  
]  !
=" #
	debugData$ -
;- .
} 	
} 
} �&
CY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\ArgumentHelper.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public 

static 
class 
ArgumentHelper &
{ 
public 
static 
RequiredField #
<# $
TType$ )
>) *
Required+ 3
<3 4
TType4 9
>9 :
(: ;
); <
{ 	
return 
new 
RequiredField $
<$ %
TType% *
>* +
(+ ,
), -
;- .
} 	
public 
static 
EntityQueryType %
<% &
TType& +
>+ ,
EntityQuery- 8
<8 9
TType9 >
>> ?
(? @
)@ A
{ 	
return 
new 
EntityQueryType &
<& '
TType' ,
>, -
(- .
). /
;/ 0
} 	
} 
public 

class 
RequiredField 
< 
TType $
>$ %
{ 
public 
Type 
Type 
{ 
get 
; 
}  !
public 
TType 
Value 
{ 
get  
;  !
set" %
;% &
}' (
public!! 
RequiredField!! 
(!! 
)!! 
{"" 	
Type## 
=## 
typeof## 
(## 
TType## 
)##  
;##  !
Value$$ 
=$$ 
default$$ 
($$ 
TType$$ !
)$$! "
;$$" #
}%% 	
public'' 
RequiredField'' 
('' 
TType'' "
value''# (
)''( )
{(( 	
Type)) 
=)) 
typeof)) 
()) 
TType)) 
)))  
;))  !
Value** 
=** 
value** 
;** 
}++ 	
public-- 
static-- 
implicit-- 
operator-- '
TType--( -
(--- .
RequiredField--. ;
<--; <
TType--< A
>--A B
field--C H
)--H I
{.. 	
return// 
field// 
.// 
Value// 
;// 
}00 	
public22 
static22 
implicit22 
operator22 '
RequiredField22( 5
<225 6
TType226 ;
>22; <
(22< =
TType22= B
value22C H
)22H I
{33 	
return44 
new44 
RequiredField44 $
<44$ %
TType44% *
>44* +
(44+ ,
value44, 1
)441 2
;442 3
}55 	
public77 
override77 
string77 
ToString77 '
(77' (
)77( )
{88 	
return99 
Value99 
.99 
ToString99 !
(99! "
)99" #
;99# $
}:: 	
};; 
public== 

class== 
EntityQueryType==  
<==  !
TType==! &
>==& '
:==( )
BaseEntityQueryType==* =
{>> 
publicCC 

ExpressionCC 
<CC 
FuncCC 
<CC 
TTypeCC $
,CC$ %
boolCC& *
>CC* +
>CC+ ,
QueryCC- 2
{CC3 4
getCC5 8
;CC8 9
setCC: =
;CC= >
}CC? @
publicII 
boolII 
HasValueII 
{II 
getII "
;II" #
setII$ '
;II' (
}II) *
publicKK 
EntityQueryTypeKK 
(KK 
)KK  
{LL 	
thisMM 
.MM 
	QueryTypeMM 
=MM 
typeofMM #
(MM# $
TTypeMM$ )
)MM) *
;MM* +
}NN 	
publicPP 
staticPP 
implicitPP 
operatorPP '

ExpressionPP( 2
<PP2 3
FuncPP3 7
<PP7 8
TTypePP8 =
,PP= >
boolPP? C
>PPC D
>PPD E
(PPE F
EntityQueryTypePPF U
<PPU V
TTypePPV [
>PP[ \
qPP] ^
)PP^ _
{QQ 	
returnRR 
qRR 
.RR 
QueryRR 
;RR 
}SS 	
}TT 
publicVV 

classVV 
BaseEntityQueryTypeVV $
{WW 
publicXX 
TypeXX 
	QueryTypeXX 
{XX 
getXX  #
;XX# $
	protectedXX% .
setXX/ 2
;XX2 3
}XX4 5
}YY 
}ZZ �
KY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\EntityQuerySchemaError.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public 

class "
EntityQuerySchemaError '
:( )
	Exception* 3
{ 
public "
EntityQuerySchemaError %
(% &
)& '
{ 	
}		 	
public "
EntityQuerySchemaError %
(% &
string& ,
message- 4
)4 5
:6 7
base8 <
(< =
message= D
)D E
{ 	
} 	
public "
EntityQuerySchemaError %
(% &
string& ,
message- 4
,4 5
	Exception6 ?
innerException@ N
)N O
:P Q
baseR V
(V W
messageW ^
,^ _
innerException` n
)n o
{ 	
} 	
} 
} �O
:Y:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\Field.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{		 
public 

class 
Field 
: 
IMethodType $
{ 
private 
readonly 

Dictionary #
<# $
string$ *
,* +
Type, 0
>0 1
allArguments2 >
=? @
newA D

DictionaryE O
<O P
stringP V
,V W
TypeX \
>\ ]
(] ^
)^ _
;_ `
public 
string 
Name 
{ 
get  
;  !
internal" *
set+ .
;. /
}0 1
public 
ParameterExpression "

FieldParam# -
{. /
get0 3
;3 4
private5 <
set= @
;@ A
}B C
internal 
Field 
( 
string 
name "
," #
LambdaExpression$ 4
resolve5 <
,< =
string> D
descriptionE P
,P Q
stringR X
returnSchemaTypeY i
=j k
nulll p
)p q
{ 	
Name 
= 
name 
; 
Resolve 
= 
resolve 
. 
Body "
;" #
Description 
= 
description %
;% &

FieldParam 
= 
resolve  
.  !

Parameters! +
.+ ,
First, 1
(1 2
)2 3
;3 4
ReturnTypeSingle 
= 
returnSchemaType /
;/ 0
IsEnumerable 
= 
resolve "
." #
Body# '
.' (
Type( ,
., -
IsEnumerableOrArray- @
(@ A
)A B
;B C
if 
( 
ReturnTypeSingle  
==! #
null$ (
)( )
{ 
if 
( 
IsEnumerable  
)  !
{ 
if 
( 
! 
resolve  
.  !
Body! %
.% &
Type& *
.* +
IsArray+ 2
&&3 5
resolve6 =
.= >
Body> B
.B C
TypeC G
.G H
GetGenericArgumentsH [
([ \
)\ ]
.] ^
Count^ c
(c d
)d e
==f h
$numi j
)j k
{   
throw!! 
new!! !
ArgumentException!!" 3
(!!3 4
$"!!4 6
	We think !!6 ?
{!!? @
resolve!!@ G
.!!G H
Body!!H L
.!!L M
Type!!M Q
}!!Q RO
B is IEnumerable<> or an array but didn't find it's enumerable type	!!R �
"
!!� �
)
!!� �
;
!!� �
}"" 
ReturnTypeSingle## $
=##% &
resolve##' .
.##. /
Body##/ 3
.##3 4
Type##4 8
.##8 9$
GetEnumerableOrArrayType##9 Q
(##Q R
)##R S
.##S T
Name##T X
;##X Y
}$$ 
else%% 
{&& 
ReturnTypeSingle'' $
=''% &
resolve''' .
.''. /
Body''/ 3
.''3 4
Type''4 8
.''8 9
Name''9 =
;''= >
}(( 
})) 
}** 	
public,, 
Field,, 
(,, 
string,, 
name,,  
,,,  !
LambdaExpression,," 2
resolve,,3 :
,,,: ;
string,,< B
description,,C N
,,,N O
string,,P V
returnSchemaType,,W g
,,,g h
object,,i o
argTypes,,p x
),,x y
:,,z {
this	,,| �
(
,,� �
name
,,� �
,
,,� �
resolve
,,� �
,
,,� �
description
,,� �
,
,,� �
returnSchemaType
,,� �
)
,,� �
{-- 	
this.. 
... 
ArgumentTypesObject.. $
=..% &
argTypes..' /
;../ 0
this// 
.// 
allArguments// 
=// 
argTypes//  (
.//( )
GetType//) 0
(//0 1
)//1 2
.//2 3
GetProperties//3 @
(//@ A
)//A B
.//B C
ToDictionary//C O
(//O P
p//P Q
=>//R T
p//U V
.//V W
Name//W [
,//[ \
p//] ^
=>//_ a
p//b c
.//c d
PropertyType//d p
)//p q
;//q r
argTypes00 
.00 
GetType00 
(00 
)00 
.00 
	GetFields00 (
(00( )
)00) *
.00* +
ToDictionary00+ 7
(007 8
p008 9
=>00: <
p00= >
.00> ?
Name00? C
,00C D
p00E F
=>00G I
p00J K
.00K L
	FieldType00L U
)00U V
.00V W
ToList00W ]
(00] ^
)00^ _
.00_ `
ForEach00` g
(00g h
kvp00h k
=>00l n
allArguments00o {
.00{ |
Add00| 
(	00 �
kvp
00� �
.
00� �
Key
00� �
,
00� �
kvp
00� �
.
00� �
Value
00� �
)
00� �
)
00� �
;
00� �
}11 	
public33 

Expression33 
Resolve33 !
{33" #
get33$ '
;33' (
private33) 0
set331 4
;334 5
}336 7
public44 
string44 
Description44 !
{44" #
get44$ '
;44' (
private44) 0
set441 4
;444 5
}446 7
public55 
string55 
ReturnTypeSingle55 &
{55' (
get55) ,
;55, -
private55. 5
set556 9
;559 :
}55; <
public77 
bool77 
IsEnumerable77  
{77! "
get77# &
;77& '
}77( )
public99 
object99 
ArgumentTypesObject99 )
{99* +
get99, /
;99/ 0
private991 8
set999 <
;99< =
}99> ?
public:: 
IDictionary:: 
<:: 
string:: !
,::! "
Type::# '
>::' (
	Arguments::) 2
=>::3 5
allArguments::6 B
;::B C
public<< 
IEnumerable<< 
<<< 
string<< !
><<! "!
RequiredArgumentNames<<# 8
{== 	
get>> 
{?? 
if@@ 
(@@ 
ArgumentTypesObject@@ '
==@@( *
null@@+ /
)@@/ 0
returnAA 
newAA 
ListAA #
<AA# $
stringAA$ *
>AA* +
(AA+ ,
)AA, -
;AA- .
varCC 
requiredCC 
=CC 
ArgumentTypesObjectCC 2
.CC2 3
GetTypeCC3 :
(CC: ;
)CC; <
.CC< =
GetTypeInfoCC= H
(CCH I
)CCI J
.CCJ K
	GetFieldsCCK T
(CCT U
)CCU V
.CCV W
WhereCCW \
(CC\ ]
fCC] ^
=>CC_ a
fCCb c
.CCc d
	FieldTypeCCd m
.CCm n%
IsConstructedGenericType	CCn �
&&
CC� �
f
CC� �
.
CC� �
	FieldType
CC� �
.
CC� �&
GetGenericTypeDefinition
CC� �
(
CC� �
)
CC� �
==
CC� �
typeof
CC� �
(
CC� �
RequiredField
CC� �
<
CC� �
>
CC� �
)
CC� �
)
CC� �
.
CC� �
Select
CC� �
(
CC� �
f
CC� �
=>
CC� �
f
CC� �
.
CC� �
Name
CC� �
)
CC� �
;
CC� �
varDD 
requiredPropsDD !
=DD" #
ArgumentTypesObjectDD$ 7
.DD7 8
GetTypeDD8 ?
(DD? @
)DD@ A
.DDA B
GetTypeInfoDDB M
(DDM N
)DDN O
.DDO P
GetPropertiesDDP ]
(DD] ^
)DD^ _
.DD_ `
WhereDD` e
(DDe f
fDDf g
=>DDh j
fDDk l
.DDl m
PropertyTypeDDm y
.DDy z%
IsConstructedGenericType	DDz �
&&
DD� �
f
DD� �
.
DD� �
PropertyType
DD� �
.
DD� �&
GetGenericTypeDefinition
DD� �
(
DD� �
)
DD� �
==
DD� �
typeof
DD� �
(
DD� �
RequiredField
DD� �
<
DD� �
>
DD� �
)
DD� �
)
DD� �
.
DD� �
Select
DD� �
(
DD� �
f
DD� �
=>
DD� �
f
DD� �
.
DD� �
Name
DD� �
)
DD� �
;
DD� �
returnEE 
requiredEE 
.EE  
ConcatEE  &
(EE& '
requiredPropsEE' 4
)EE4 5
.EE5 6
ToListEE6 <
(EE< =
)EE= >
;EE> ?
}FF 
}GG 	
publicII 
TypeII 
ReturnTypeClrII !
=>II" $
ResolveII% ,
.II, -
TypeII- 1
;II1 2
publicKK 
boolKK 
HasArgumentByNameKK %
(KK% &
stringKK& ,
argNameKK- 4
)KK4 5
{LL 	
returnMM 
allArgumentsMM 
.MM  
ContainsKeyMM  +
(MM+ ,
argNameMM, 3
)MM3 4
;MM4 5
}NN 	
publicPP 
TypePP 
GetArgumentTypePP #
(PP# $
stringPP$ *
argNamePP+ 2
)PP2 3
{QQ 	
returnRR 
allArgumentsRR 
[RR  
argNameRR  '
]RR' (
;RR( )
}SS 	
}TT 
}UU �
KY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\GraphQLIgnoreAttribute.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public 

class "
GraphQLIgnoreAttribute '
:( )
	Attribute* 3
{		 
}

 
} �
MY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\GraphQLMutationAttribute.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public 

class $
GraphQLMutationAttribute )
:* +
	Attribute, 5
{ 
private 
string 
description "
;" #
public		 $
GraphQLMutationAttribute		 '
(		' (
string		( .
description		/ :
=		; <
null		= A
)		A B
{

 	
this 
. 
Description 
= 
description *
;* +
} 	
public 
string 
Description !
{" #
get$ '
=>( *
description+ 6
;6 7
set8 ;
=>< >
description? J
=K L
valueM R
;R S
}T U
} 
} �	
@Y:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\IMethodType.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public 

	interface 
IMethodType  
{ 
IDictionary 
< 
string 
, 
Type  
>  !
	Arguments" +
{, -
get. 1
;1 2
}3 4
bool		 
IsEnumerable		 
{		 
get		 
;		  
}		! "
string

 
Name

 
{

 
get

 
;

 
}

 
Type 
ReturnTypeClr 
{ 
get  
;  !
}" #
string 
Description 
{ 
get  
;  !
}" #
string 
ReturnTypeSingle 
{  !
get" %
;% &
}' (
Type 
GetArgumentType 
( 
string #
argName$ +
)+ ,
;, -
bool 
HasArgumentByName 
( 
string %
argName& -
)- .
;. /
} 
} �
DY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\ISchemaProvider.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public 

	interface 
ISchemaProvider $
{ 
Type 
ContextType 
{ 
get 
; 
}  !
IEnumerable 
< 
string 
> 
CustomScalarTypes -
{. /
get0 3
;3 4
}5 6
bool 
TypeHasField 
( 
string  
typeName! )
,) *
string+ 1

identifier2 <
,< =
IEnumerable> I
<I J
stringJ P
>P Q
	fieldArgsR [
)[ \
;\ ]
bool 
TypeHasField 
( 
Type 
type #
,# $
string% +

identifier, 6
,6 7
IEnumerable8 C
<C D
stringD J
>J K
	fieldArgsL U
)U V
;V W
bool 
HasType 
( 
string 
typeName $
)$ %
;% &
bool 
HasType 
( 
Type 
type 
) 
;  
ISchemaType 
Type 
( 
string 
name  $
)$ %
;% &
string 
GetActualFieldName !
(! "
string" (
typeName) 1
,1 2
string3 9

identifier: D
)D E
;E F
ExpressionResult(( !
GetExpressionForField(( .
(((. /

Expression((/ 9
context((: A
,((A B
string((C I
typeName((J R
,((R S
string((T Z
field(([ `
,((` a

Dictionary((b l
<((l m
string((m s
,((s t
ExpressionResult	((u �
>
((� �
args
((� �
)
((� �
;
((� �
string)) (
GetSchemaTypeNameForRealType)) +
())+ ,
Type)), 0
type))1 5
)))5 6
;))6 7
IMethodType** 
GetFieldType**  
(**  !

Expression**! +
context**, 3
,**3 4
string**5 ;
field**< A
)**A B
;**B C
bool++ 
HasMutation++ 
(++ 
string++ 
method++  &
)++& '
;++' (
string,, 
GetGraphQLSchema,, 
(,,  
),,  !
;,,! "
IEnumerable11 
<11 
Field11 
>11 
GetQueryFields11 )
(11) *
)11* +
;11+ ,
IEnumerable66 
<66 
ISchemaType66 
>66  
GetNonContextTypes66! 3
(663 4
)664 5
;665 6
IEnumerable88 
<88 
IMethodType88 
>88  
GetMutations88! -
(88- .
)88. /
;88/ 0
void?? 
AddCustomScalarType??  
(??  !
Type??! %
clrType??& -
,??- .
string??/ 5
gqlTypeName??6 A
)??A B
;??B C
}@@ 
}AA �
@Y:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\ISchemaType.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public 

	interface 
ISchemaType  
{ 
Type 
ContextType 
{ 
get 
; 
}  !
string		 
Name		 
{		 
get		 
;		 
}		 
string

 
Description

 
{

 
get

  
;

  !
}

" #
bool 
IsInput 
{ 
get 
; 
} 
Field 
GetField 
( 
string 

identifier (
)( )
;) *
IEnumerable 
< 
Field 
> 
	GetFields $
($ %
)% &
;& '
bool 
HasField 
( 
string 

identifier '
)' (
;( )
void 
	AddFields 
( 
List 
< 
Field !
>! "
fields# )
)) *
;* +
void 
AddField 
( 
Field 
field !
)! "
;" #
void 
RemoveField 
( 
string 
name  $
)$ %
;% &
} 
} ��
IY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\MappedSchemaProvider.cs
	namespace

 	
EntityGraphQL


 
.

 
Schema

 
{ 
public 

class  
MappedSchemaProvider %
<% &
TContextType& 2
>2 3
:4 5
ISchemaProvider6 E
{ 
	protected 

Dictionary 
< 
string #
,# $
ISchemaType% 0
>0 1
_types2 8
=9 :
new; >

Dictionary? I
<I J
stringJ P
,P Q
ISchemaTypeR ]
>] ^
(^ _
)_ `
;` a
	protected 

Dictionary 
< 
string #
,# $
IMethodType% 0
>0 1

_mutations2 <
== >
new? B

DictionaryC M
<M N
stringN T
,T U
IMethodTypeV a
>a b
(b c
)c d
;d e
	protected 

Dictionary 
< 
Type !
,! "
string# )
>) *
_customTypeMappings+ >
=? @
newA D

DictionaryE O
<O P
TypeP T
,T U
stringV \
>\ ]
(] ^
)^ _
;_ `
private 
readonly 
string 
_queryContextName  1
;1 2
private 
readonly 

Dictionary #
<# $
Type$ (
,( )
string* 0
>0 1!
_customScalarMappings2 G
=H I
newJ M

DictionaryN X
<X Y
TypeY ]
,] ^
string_ e
>e f
(f g
)g h
;h i
public 
IEnumerable 
< 
string !
>! "
CustomScalarTypes# 4
=>5 7!
_customScalarMappings8 M
.M N
ValuesN T
;T U
public  
MappedSchemaProvider #
(# $
)$ %
{ 	
var 
queryContext 
= 
new "

SchemaType# -
<- .
TContextType. :
>: ;
(; <
typeof< B
(B C
TContextTypeC O
)O P
.P Q
NameQ U
,U V
$strW e
)e f
;f g
_queryContextName 
= 
queryContext  ,
., -
Name- 1
;1 2
_types 
. 
Add 
( 
queryContext #
.# $
Name$ (
,( )
queryContext* 6
)6 7
;7 8
AddType   
<   
Models   
.   

InputValue   %
>  % &
(  & '
$str  ' 5
,  5 6
$str	  7 �
)
  � �
.
  � �
AddAllFields
  � �
(
  � �
)
  � �
;
  � �
AddType!! 
<!! 
Models!! 
.!! 

Directives!! %
>!!% &
(!!& '
$str!!' 4
,!!4 5
$str!!6 T
)!!T U
.!!U V
AddAllFields!!V b
(!!b c
)!!c d
;!!d e
AddType"" 
<"" 
Models"" 
."" 
	EnumValue"" $
>""$ %
(""% &
$str""& 3
,""3 4
$str""5 N
)""N O
.""O P
AddAllFields""P \
(""\ ]
)""] ^
;""^ _
AddType## 
<## 
Models## 
.## 
Field##  
>##  !
(##! "
$str##" +
,##+ ,
$str##- G
)##G H
.##H I
AddAllFields##I U
(##U V
)##V W
;##W X
AddType$$ 
<$$ 
Models$$ 
.$$ 
Schema$$ !
>$$! "
($$" #
$str$$# -
,$$- .
$str	$$/ �
)
$$� �
.
$$� �
AddAllFields
$$� �
(
$$� �
)
$$� �
;
$$� �
AddType%% 
<%% 
Models%% 
.%% 
SubscriptionType%% +
>%%+ ,
(%%, -
$str%%- N
)%%N O
.%%O P
AddAllFields%%P \
(%%\ ]
)%%] ^
;%%^ _
AddType&& 
<&& 
Models&& 
.&& 
TypeElement&& &
>&&& '
(&&' (
$str&&( 0
,&&0 1
$str&&2 K
)&&K L
.&&L M
AddAllFields&&M Y
(&&Y Z
)&&Z [
;&&[ \
Type(( 
<(( 
Models(( 
.(( 
TypeElement(( #
>((# $
((($ %
$str((% -
)((- .
.((. /
ReplaceField((/ ;
(((; <
$str((< H
,((H I
new((J M
{((N O
includeDeprecated((P a
=((b c
false((d i
}((j k
,((k l
()) 
t)) 
,)) 
p)) 
))) 
=>)) 
t)) 
.)) 

EnumValues)) &
.))& '
Where))' ,
()), -
f))- .
=>))/ 1
p))2 3
.))3 4
includeDeprecated))4 E
?))F G
f))H I
.))I J
IsDeprecated))J V
||))W Y
!))Z [
f))[ \
.))\ ]
IsDeprecated))] i
:))j k
!))l m
f))m n
.))n o
IsDeprecated))o {
))){ |
.))| }
ToList	))} �
(
))� �
)
))� �
,
))� �
$str
))� �
)
))� �
;
))� �+
SetupIntrospectionTypesAndField++ +
(+++ ,
)++, -
;++- .
}11 	
private33 
void33 +
SetupIntrospectionTypesAndField33 4
(334 5
)335 6
{44 	
var55 
allTypeMappings55 
=55  !
SchemaGenerator55" 1
.551 2
DefaultTypeMappings552 E
.55E F
ToDictionary55F R
(55R S
k55S T
=>55U W
k55X Y
.55Y Z
Key55Z ]
,55] ^
v55_ `
=>55a c
v55d e
.55e f
Value55f k
.55k l
Trim55l p
(55p q
$char55q t
)55t u
)55u v
;55v w
foreach77 
(77 
var77 
item77 
in77  
_customTypeMappings77! 4
)774 5
{88 
allTypeMappings99 
[99  
item99  $
.99$ %
Key99% (
]99( )
=99* +
item99, 0
.990 1
Value991 6
;996 7
}:: 
foreach;; 
(;; 
var;; 
item;; 
in;;  !
_customScalarMappings;;! 6
);;6 7
{<< 
allTypeMappings== 
[==  
item==  $
.==$ %
Key==% (
]==( )
===* +
item==, 0
.==0 1
Value==1 6
;==6 7
}>> 
TypeAA 
<AA 
ModelsAA 
.AA 
TypeElementAA #
>AA# $
(AA$ %
$strAA% -
)AA- .
.AA. /
ReplaceFieldAA/ ;
(AA; <
$strAA< D
,AAD E
newAAF I
{AAJ K
includeDeprecatedAAL ]
=AA^ _
falseAA` e
}AAf g
,AAg h
(BB 
tBB 
,BB 
pBB 
)BB 
=>BB 
SchemaIntrospectionBB -
.BB- .
BuildFieldsForTypeBB. @
(BB@ A
thisBBA E
,BBE F
allTypeMappingsBBG V
,BBV W
tBBX Y
.BBY Z
NameBBZ ^
)BB^ _
.BB_ `
WhereBB` e
(BBe f
fBBf g
=>BBh j
pBBk l
.BBl m
includeDeprecatedBBm ~
?	BB �
f
BB� �
.
BB� �
IsDeprecated
BB� �
||
BB� �
!
BB� �
f
BB� �
.
BB� �
IsDeprecated
BB� �
:
BB� �
!
BB� �
f
BB� �
.
BB� �
IsDeprecated
BB� �
)
BB� �
.
BB� �
ToList
BB� �
(
BB� �
)
BB� �
,
BB� �
$str
BB� �
)
BB� �
;
BB� �
ReplaceFieldEE 
(EE 
$strEE #
,EE# $
dbEE% '
=>EE( *
SchemaIntrospectionEE+ >
.EE> ?
MakeEE? C
(EEC D
thisEED H
,EEH I
allTypeMappingsEEJ Y
)EEY Z
,EEZ [
$strEE\ y
,EEy z
$str	EE{ �
)
EE� �
;
EE� �
ReplaceFieldFF 
(FF 
$strFF !
,FF! "
newFF# &
{FF' (
nameFF) -
=FF. /
ArgumentHelperFF0 >
.FF> ?
RequiredFF? G
<FFG H
stringFFH N
>FFN O
(FFO P
)FFP Q
}FFR S
,FFS T
(FFU V
dbFFV X
,FFX Y
pFFZ [
)FF[ \
=>FF] _
SchemaIntrospectionFF` s
.FFs t
MakeFFt x
(FFx y
thisFFy }
,FF} ~
allTypeMappings	FF �
)
FF� �
.
FF� �
Types
FF� �
.
FF� �
Where
FF� �
(
FF� �
s
FF� �
=>
FF� �
s
FF� �
.
FF� �
Name
FF� �
==
FF� �
p
FF� �
.
FF� �
name
FF� �
)
FF� �
.
FF� �
ToList
FF� �
(
FF� �
)
FF� �
,
FF� �
$str
FF� �
,
FF� �
$str
FF� �
)
FF� �
;
FF� �
}GG 	
publicPP 

SchemaTypePP 
<PP 
	TBaseTypePP #
>PP# $
AddTypePP% ,
<PP, -
	TBaseTypePP- 6
>PP6 7
(PP7 8
stringPP8 >
namePP? C
,PPC D
stringPPE K
descriptionPPL W
)PPW X
{QQ 	
returnRR 	
AddTypeRR
 
<RR 
	TBaseTypeRR 
>RR 
(RR 
nameRR !
,RR! "
descriptionRR# .
,RR. /
nullRR0 4
)RR4 5
;RR5 6
}SS 	
public]] 

SchemaType]] 
<]] 
	TBaseType]] #
>]]# $
AddType]]% ,
<]], -
	TBaseType]]- 6
>]]6 7
(]]7 8
string]]8 >
name]]? C
,]]C D
string]]E K
description]]L W
,]]W X

Expression]]Y c
<]]c d
Func]]d h
<]]h i
	TBaseType]]i r
,]]r s
bool]]t x
>]]x y
>]]y z
filter	]]{ �
)
]]� �
{^^ 	
var__ 
tt__ 	
=__
 
new__ 

SchemaType__ 
<__ 
	TBaseType__ $
>__$ %
(__% &
name__& *
,__* +
description__, 7
,__7 8
filter__9 ?
)__? @
;__@ A
_types`` 
.`` 
Add`` 
(`` 
name`` 
,`` 
tt`` 
)``  
;``  !
returnaa 	
ttaa
 
;aa 
}bb 	
publicdd 

SchemaTypedd 
<dd 
objectdd  
>dd  !
AddTypedd" )
(dd) *
Typedd* .
contextTypedd/ :
,dd: ;
stringdd< B
nameddC G
,ddG H
stringddI O
descriptionddP [
)dd[ \
{ee 	
varff 
ttff 	
=ff
 
newff 

SchemaTypeff 
<ff 
objectff !
>ff! "
(ff" #
contextTypeff# .
,ff. /
nameff0 4
,ff4 5
descriptionff6 A
,ffA B
nullffC G
)ffG H
;ffH I
_typesgg 
.gg 
Addgg 
(gg 
namegg 
,gg 
ttgg 
)gg  
;gg  !
returnhh 	
tthh
 
;hh 
}ii 	
publickk 

SchemaTypekk 
<kk 
	TBaseTypekk #
>kk# $
AddInputTypekk% 1
<kk1 2
	TBaseTypekk2 ;
>kk; <
(kk< =
stringkk= C
namekkD H
,kkH I
stringkkJ P
descriptionkkQ \
)kk\ ]
{ll 	
varmm 
ttmm 
=mm 
newmm 

SchemaTypemm #
<mm# $
	TBaseTypemm$ -
>mm- .
(mm. /
namemm/ 3
,mm3 4
descriptionmm5 @
,mm@ A
nullmmB F
,mmF G
truemmH L
)mmL M
;mmM N
_typesnn 
.nn 
Addnn 
(nn 
namenn 
,nn 
ttnn 
)nn  
;nn  !
returnoo 	
ttoo
 
;oo 
}pp 	
publicww 
voidww 
AddMutationFromww #
<ww# $
TTypeww$ )
>ww) *
(ww* +
TTypeww+ 0!
mutationClassInstanceww1 F
)wwF G
{xx 	
foreachyy 
(yy 
varyy 
methodyy 
inyy  "!
mutationClassInstanceyy# 8
.yy8 9
GetTypeyy9 @
(yy@ A
)yyA B
.yyB C

GetMethodsyyC M
(yyM N
)yyN O
)yyO P
{zz 
var{{ 
	attribute{{ 
={{ 
method{{  &
.{{& '
GetCustomAttribute{{' 9
({{9 :
typeof{{: @
({{@ A$
GraphQLMutationAttribute{{A Y
){{Y Z
){{Z [
as{{\ ^$
GraphQLMutationAttribute{{_ w
;{{w x
if|| 
(|| 
	attribute|| 
!=||  
null||! %
)||% &
{}} 
string~~ 
name~~ 
=~~  !
SchemaGenerator~~" 1
.~~1 2"
ToCamelCaseStartsLower~~2 H
(~~H I
method~~I O
.~~O P
Name~~P T
)~~T U
;~~U V
var 
mutationType $
=% &
new' *
MutationType+ 7
(7 8
name8 <
,< =
_types> D
[D E(
GetSchemaTypeNameForRealTypeE a
(a b
methodb h
.h i

ReturnTypei s
)s t
]t u
,u v"
mutationClassInstance	w �
,
� �
method
� �
,
� �
	attribute
� �
.
� �
Description
� �
)
� �
;
� �

_mutations
�� 
[
�� 
name
�� #
]
��# $
=
��% &
mutationType
��' 3
;
��3 4
}
�� 
}
�� 
}
�� 	
public
�� 
bool
�� 
HasMutation
�� 
(
��  
string
��  &
method
��' -
)
��- .
{
�� 	
return
�� 

_mutations
�� 
.
�� 
ContainsKey
�� )
(
��) *
method
��* 0
)
��0 1
;
��1 2
}
�� 	
public
�� 
void
�� 
AddTypeMapping
�� "
<
��" #
TFrom
��# (
>
��( )
(
��) *
string
��* 0
gqlType
��1 8
)
��8 9
{
�� 	!
_customTypeMappings
�� 
.
��  
Add
��  #
(
��# $
typeof
��$ *
(
��* +
TFrom
��+ 0
)
��0 1
,
��1 2
gqlType
��3 :
)
��: ;
;
��; <-
SetupIntrospectionTypesAndField
�� +
(
��+ ,
)
��, -
;
��- .
}
�� 	
public
�� 

SchemaType
�� 
<
�� 
	TBaseType
�� #
>
��# $
AddType
��% ,
<
��, -
	TBaseType
��- 6
>
��6 7
(
��7 8
string
��8 >
description
��? J
,
��J K

Expression
��L V
<
��V W
Func
��W [
<
��[ \
	TBaseType
��\ e
,
��e f
bool
��g k
>
��k l
>
��l m
filter
��n t
=
��u v
null
��w {
)
��{ |
{
�� 	
var
�� 
name
�� 
=
�� 
typeof
�� 
(
�� 
	TBaseType
�� '
)
��' (
.
��( )
Name
��) -
;
��- .
return
�� 
AddType
�� 
(
�� 
name
�� 
,
��  
description
��! ,
,
��, -
filter
��. 4
)
��4 5
;
��5 6
}
�� 	
public
�� 
void
�� 
AddField
�� 
(
�� 

Expression
�� '
<
��' (
Func
��( ,
<
��, -
TContextType
��- 9
,
��9 :
object
��; A
>
��A B
>
��B C
	selection
��D M
,
��M N
string
��O U
description
��V a
,
��a b
string
��c i
returnSchemaType
��j z
=
��{ |
null��} �
)��� �
{
�� 	
var
�� 
exp
�� 
=
�� 
ExpressionUtil
�� $
.
��$ %)
CheckAndGetMemberExpression
��% @
(
��@ A
	selection
��A J
)
��J K
;
��K L
AddField
�� 
(
�� 
SchemaGenerator
�� $
.
��$ %$
ToCamelCaseStartsLower
��% ;
(
��; <
exp
��< ?
.
��? @
Member
��@ F
.
��F G
Name
��G K
)
��K L
,
��L M
	selection
��N W
,
��W X
description
��Y d
,
��d e
returnSchemaType
��f v
)
��v w
;
��w x
}
�� 	
public
�� 
void
�� 
AddField
�� 
(
�� 
string
�� #
name
��$ (
,
��( )

Expression
��* 4
<
��4 5
Func
��5 9
<
��9 :
TContextType
��: F
,
��F G
object
��H N
>
��N O
>
��O P
	selection
��Q Z
,
��Z [
string
��\ b
description
��c n
,
��n o
string
��p v
returnSchemaType��w �
=��� �
null��� �
)��� �
{
�� 	
Type
�� 
<
�� 
TContextType
�� 
>
�� 
(
�� 
)
��  
.
��  !
AddField
��! )
(
��) *
name
��* .
,
��. /
	selection
��0 9
,
��9 :
description
��; F
,
��F G
returnSchemaType
��H X
)
��X Y
;
��Y Z
}
�� 	
public
�� 
void
�� 
ReplaceField
��  
<
��  !
TReturn
��! (
>
��( )
(
��) *
string
��* 0
name
��1 5
,
��5 6

Expression
��7 A
<
��A B
Func
��B F
<
��F G
TContextType
��G S
,
��S T
TReturn
��U \
>
��\ ]
>
��] ^!
selectionExpression
��_ r
,
��r s
string
��t z
description��{ �
,��� �
string��� � 
returnSchemaType��� �
=��� �
null��� �
)��� �
{
�� 	
Type
�� 
<
�� 
TContextType
�� 
>
�� 
(
�� 
)
��  
.
��  !
RemoveField
��! ,
(
��, -
name
��- 1
)
��1 2
;
��2 3
Type
�� 
<
�� 
TContextType
�� 
>
�� 
(
�� 
)
��  
.
��  !
AddField
��! )
(
��) *
name
��* .
,
��. /!
selectionExpression
��0 C
,
��C D
description
��E P
,
��P Q
returnSchemaType
��R b
)
��b c
;
��c d
}
�� 	
public
�� 
void
�� 
ReplaceField
��  
<
��  !
TParams
��! (
,
��( )
TReturn
��* 1
>
��1 2
(
��2 3
string
��3 9
name
��: >
,
��> ?
TParams
��@ G
argTypes
��H P
,
��P Q

Expression
��R \
<
��\ ]
Func
��] a
<
��a b
TContextType
��b n
,
��n o
TParams
��p w
,
��w x
TReturn��y �
>��� �
>��� �#
selectionExpression��� �
,��� �
string��� �
description��� �
,��� �
string��� � 
returnSchemaType��� �
=��� �
null��� �
)��� �
{
�� 	
Type
�� 
<
�� 
TContextType
�� 
>
�� 
(
�� 
)
��  
.
��  !
RemoveField
��! ,
(
��, -
name
��- 1
)
��1 2
;
��2 3
Type
�� 
<
�� 
TContextType
�� 
>
�� 
(
�� 
)
��  
.
��  !
AddField
��! )
(
��) *
name
��* .
,
��. /
argTypes
��0 8
,
��8 9!
selectionExpression
��: M
,
��M N
description
��O Z
,
��Z [
returnSchemaType
��\ l
)
��l m
;
��m n
}
�� 	
public
�� 
void
�� 
AddField
�� 
<
�� 
TParams
�� $
,
��$ %
TReturn
��& -
>
��- .
(
��. /
string
��/ 5
name
��6 :
,
��: ;
TParams
��< C
argTypes
��D L
,
��L M

Expression
��N X
<
��X Y
Func
��Y ]
<
��] ^
TContextType
��^ j
,
��j k
TParams
��l s
,
��s t
TReturn
��u |
>
��| }
>
��} ~"
selectionExpression�� �
,��� �
string��� �
description��� �
,��� �
string��� � 
returnSchemaType��� �
=��� �
null��� �
)��� �
{
�� 	
Type
�� 
<
�� 
TContextType
�� 
>
�� 
(
�� 
)
��  
.
��  !
AddField
��! )
(
��) *
name
��* .
,
��. /
argTypes
��0 8
,
��8 9!
selectionExpression
��: M
,
��M N
description
��O Z
,
��Z [
returnSchemaType
��\ l
)
��l m
;
��m n
}
�� 	
public
�� 
void
�� 
AddField
�� 
(
�� 
Field
�� "
field
��# (
)
��( )
{
�� 	
_types
�� 
[
�� 
_queryContextName
�� $
]
��$ %
.
��% &
AddField
��& .
(
��. /
field
��/ 4
)
��4 5
;
��5 6
}
�� 	
public
�� 

SchemaType
�� 
<
�� 
TType
�� 
>
��  
Type
��! %
<
��% &
TType
��& +
>
��+ ,
(
��, -
)
��- .
{
�� 	
return
�� 
(
�� 

SchemaType
�� 
<
�� 
TType
�� $
>
��$ %
)
��% &
_types
��& ,
[
��, -
typeof
��- 3
(
��3 4
TType
��4 9
)
��9 :
.
��: ;
Name
��; ?
]
��? @
;
��@ A
}
�� 	
public
�� 

SchemaType
�� 
<
�� 
TType
�� 
>
��  
Type
��! %
<
��% &
TType
��& +
>
��+ ,
(
��, -
string
��- 3
typeName
��4 <
)
��< =
{
�� 	
return
�� 
(
�� 

SchemaType
�� 
<
�� 
TType
�� $
>
��$ %
)
��% &
_types
��& ,
[
��, -
typeName
��- 5
]
��5 6
;
��6 7
}
�� 	
public
�� 
ISchemaType
�� 
Type
�� 
(
��  
string
��  &
typeName
��' /
)
��/ 0
{
�� 	
return
�� 
_types
�� 
[
�� 
typeName
�� "
]
��" #
;
��# $
}
�� 	
public
�� 
Type
�� 
ContextType
�� 
{
��  !
get
��" %
{
��& '
return
��( .
_types
��/ 5
[
��5 6
_queryContextName
��6 G
]
��G H
.
��H I
ContextType
��I T
;
��T U
}
��V W
}
��X Y
public
�� 
bool
�� 
TypeHasField
��  
(
��  !
string
��! '
typeName
��( 0
,
��0 1
string
��2 8

identifier
��9 C
,
��C D
IEnumerable
��E P
<
��P Q
string
��Q W
>
��W X
	fieldArgs
��Y b
)
��b c
{
�� 	
if
�� 
(
�� 
!
�� 
_types
�� 
.
�� 
ContainsKey
�� #
(
��# $
typeName
��$ ,
)
��, -
)
��- .
return
�� 
false
�� 
;
�� 
var
�� 
t
�� 
=
�� 
_types
�� 
[
�� 
typeName
�� #
]
��# $
;
��$ %
if
�� 
(
�� 
!
�� 
t
�� 
.
�� 
HasField
�� 
(
�� 

identifier
�� &
)
��& '
)
��' (
{
�� 
if
�� 
(
�� 
(
�� 
	fieldArgs
�� 
==
�� !
null
��" &
||
��' )
!
��* +
	fieldArgs
��+ 4
.
��4 5
Any
��5 8
(
��8 9
)
��9 :
)
��: ;
&&
��< >
t
��? @
.
��@ A
HasField
��A I
(
��I J

identifier
��J T
)
��T U
)
��U V
{
�� 
var
�� 
field
�� 
=
�� 
t
��  !
.
��! "
GetField
��" *
(
��* +

identifier
��+ 5
)
��5 6
;
��6 7
if
�� 
(
�� 
field
�� 
!=
��  
null
��! %
)
��% &
{
�� 
if
�� 
(
�� 
field
�� !
.
��! "#
RequiredArgumentNames
��" 7
.
��7 8
Count
��8 =
(
��= >
)
��> ?
>
��@ A
$num
��B C
)
��C D
{
�� 
throw
�� !
new
��" %,
EntityGraphQLCompilerException
��& D
(
��D E
$"
��E G
Field '
��G N
{
��N O

identifier
��O Y
}
��Y Z.
 ' missing required argument(s) '
��Z z
{
��z {
string��{ �
.��� �
Join��� �
(��� �
$str��� �
,��� �
field��� �
.��� �%
RequiredArgumentNames��� �
)��� �
}��� �
'��� �
"��� �
)��� �
;��� �
}
�� 
return
�� 
true
�� #
;
��# $
}
�� 
else
�� 
{
�� 
throw
�� 
new
�� !,
EntityGraphQLCompilerException
��" @
(
��@ A
$"
��A C
Field '
��C J
{
��J K

identifier
��K U
}
��U V.
 ' not found on current context '
��V v
{
��v w
typeName
��w 
}�� �
'��� �
"��� �
)��� �
;��� �
}
�� 
}
�� 
return
�� 
false
�� 
;
�� 
}
�� 
return
�� 
true
�� 
;
�� 
}
�� 	
public
�� 
bool
�� 
TypeHasField
��  
(
��  !
Type
��! %
type
��& *
,
��* +
string
��, 2

identifier
��3 =
,
��= >
IEnumerable
��? J
<
��J K
string
��K Q
>
��Q R
	fieldArgs
��S \
)
��\ ]
{
�� 	
return
�� 
TypeHasField
�� 
(
��  
type
��  $
.
��$ %
Name
��% )
,
��) *

identifier
��+ 5
,
��5 6
	fieldArgs
��7 @
)
��@ A
;
��A B
}
�� 	
public
�� 
string
��  
GetActualFieldName
�� (
(
��( )
string
��) /
typeName
��0 8
,
��8 9
string
��: @

identifier
��A K
)
��K L
{
�� 	
if
�� 
(
�� 
_types
�� 
.
�� 
ContainsKey
�� "
(
��" #
typeName
��# +
)
��+ ,
&&
��- /
_types
��0 6
[
��6 7
typeName
��7 ?
]
��? @
.
��@ A
HasField
��A I
(
��I J

identifier
��J T
)
��T U
)
��U V
return
�� 
_types
�� 
[
�� 
typeName
�� &
]
��& '
.
��' (
GetField
��( 0
(
��0 1

identifier
��1 ;
)
��; <
.
��< =
Name
��= A
;
��A B
if
�� 
(
�� 
typeName
�� 
==
�� 
_queryContextName
�� -
&&
��. 0
_types
��1 7
[
��7 8
_queryContextName
��8 I
]
��I J
.
��J K
HasField
��K S
(
��S T

identifier
��T ^
)
��^ _
)
��_ `
return
�� 
_types
�� 
[
�� 
_queryContextName
�� /
]
��/ 0
.
��0 1
GetField
��1 9
(
��9 :

identifier
��: D
)
��D E
.
��E F
Name
��F J
;
��J K
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 4
(
��4 5
$"
��5 7
Field 
��7 =
{
��= >

identifier
��> H
}
��H I$
 not found on any type
��I _
"
��_ `
)
��` a
;
��a b
}
�� 	
public
�� 
IMethodType
�� 
GetFieldType
�� '
(
��' (

Expression
��( 2
context
��3 :
,
��: ;
string
��< B
	fieldName
��C L
)
��L M
{
�� 	
if
�� 
(
�� 

_mutations
�� 
.
�� 
ContainsKey
�� &
(
��& '
	fieldName
��' 0
)
��0 1
)
��1 2
{
�� 
var
�� 
mutation
�� 
=
�� 

_mutations
�� )
[
��) *
	fieldName
��* 3
]
��3 4
;
��4 5
return
�� 
mutation
�� 
;
��  
}
�� 
if
�� 
(
�� 
_types
�� 
.
�� 
ContainsKey
�� "
(
��" #*
GetSchemaTypeNameForRealType
��# ?
(
��? @
context
��@ G
.
��G H
Type
��H L
)
��L M
)
��M N
)
��N O
{
�� 
var
�� 
field
�� 
=
�� 
_types
�� "
[
��" #*
GetSchemaTypeNameForRealType
��# ?
(
��? @
context
��@ G
.
��G H
Type
��H L
)
��L M
]
��M N
.
��N O
GetField
��O W
(
��W X
	fieldName
��X a
)
��a b
;
��b c
return
�� 
field
�� 
;
�� 
}
�� 
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 4
(
��4 5
$"
��5 7$
No field or mutation '
��7 M
{
��M N
	fieldName
��N W
}
��W X 
' found in schema.
��X j
"
��j k
)
��k l
;
��l m
}
�� 	
public
�� 
ExpressionResult
�� #
GetExpressionForField
��  5
(
��5 6

Expression
��6 @
context
��A H
,
��H I
string
��J P
typeName
��Q Y
,
��Y Z
string
��[ a
	fieldName
��b k
,
��k l

Dictionary
��m w
<
��w x
string
��x ~
,
��~  
ExpressionResult��� �
>��� �
args��� �
)��� �
{
�� 	
if
�� 
(
�� 
!
�� 
_types
�� 
.
�� 
ContainsKey
�� #
(
��# $
typeName
��$ ,
)
��, -
)
��- .
throw
�� 
new
�� $
EntityQuerySchemaError
�� 0
(
��0 1
$"
��1 3
{
��3 4
typeName
��4 <
}
��< =#
 not found in schema.
��= R
"
��R S
)
��S T
;
��T U
var
�� 
field
�� 
=
�� 
_types
�� 
[
�� 
typeName
�� '
]
��' (
.
��( )
GetField
��) 1
(
��1 2
	fieldName
��2 ;
)
��; <
;
��< =
var
�� 
result
�� 
=
�� 
new
�� 
ExpressionResult
�� -
(
��- .
field
��. 3
.
��3 4
Resolve
��4 ;
??
��< >

Expression
��? I
.
��I J
Property
��J R
(
��R S
context
��S Z
,
��Z [
	fieldName
��\ e
)
��e f
)
��f g
;
��g h
if
�� 
(
�� 
field
�� 
.
�� !
ArgumentTypesObject
�� )
!=
��* ,
null
��- 1
)
��1 2
{
�� 
var
�� 
argType
�� 
=
�� 
field
�� #
.
��# $!
ArgumentTypesObject
��$ 7
.
��7 8
GetType
��8 ?
(
��? @
)
��@ A
;
��A B
var
�� 
propVals
�� 
=
�� 
new
�� "

Dictionary
��# -
<
��- .
PropertyInfo
��. :
,
��: ;
object
��< B
>
��B C
(
��C D
)
��D E
;
��E F
var
�� 
	fieldVals
�� 
=
�� 
new
��  #

Dictionary
��$ .
<
��. /
	FieldInfo
��/ 8
,
��8 9
object
��: @
>
��@ A
(
��A B
)
��B C
;
��C D
foreach
�� 
(
�� 
var
�� 
argField
�� %
in
��& (
argType
��) 0
.
��0 1
GetProperties
��1 >
(
��> ?
)
��? @
)
��@ A
{
�� 
var
�� 
val
�� 
=
�� %
BuildArgumentFromMember
�� 5
(
��5 6
args
��6 :
,
��: ;
field
��< A
,
��A B
argField
��C K
.
��K L
Name
��L P
,
��P Q
argField
��R Z
.
��Z [
PropertyType
��[ g
,
��g h
argField
��i q
.
��q r
GetValue
��r z
(
��z {
field��{ �
.��� �#
ArgumentTypesObject��� �
)��� �
)��� �
;��� �
if
�� 
(
�� 
argField
��  
.
��  !
PropertyType
��! -
.
��- .&
IsConstructedGenericType
��. F
&&
��G I
argField
��J R
.
��R S
PropertyType
��S _
.
��_ `&
GetGenericTypeDefinition
��` x
(
��x y
)
��y z
==
��{ }
typeof��~ �
(��� �
EntityQueryType��� �
<��� �
>��� �
)��� �
)��� �
{
�� 
var
�� 
queryVal
�� $
=
��% &
argField
��' /
.
��/ 0
GetValue
��0 8
(
��8 9
field
��9 >
.
��> ?!
ArgumentTypesObject
��? R
)
��R S
;
��S T
var
�� 
hasValue
�� $
=
��% &
val
��' *
!=
��+ -
null
��. 2
;
��2 3
var
�� 
genericProp
�� '
=
��( )
queryVal
��* 2
.
��2 3
GetType
��3 :
(
��: ;
)
��; <
.
��< =
GetProperty
��= H
(
��H I
$str
��I S
)
��S T
;
��T U
genericProp
�� #
.
��# $
SetValue
��$ ,
(
��, -
queryVal
��- 5
,
��5 6
hasValue
��7 ?
)
��? @
;
��@ A
if
�� 
(
�� 
hasValue
�� $
)
��$ %
{
�� 
genericProp
�� '
=
��( )
queryVal
��* 2
.
��2 3
GetType
��3 :
(
��: ;
)
��; <
.
��< =
GetProperty
��= H
(
��H I
$str
��I P
)
��P Q
;
��Q R
genericProp
�� '
.
��' (
SetValue
��( 0
(
��0 1
queryVal
��1 9
,
��9 :
(
��; <
(
��< =
dynamic
��= D
)
��D E
val
��E H
)
��H I
.
��I J

Expression
��J T
)
��T U
;
��U V
}
�� 
propVals
��  
.
��  !
Add
��! $
(
��$ %
argField
��% -
,
��- .
queryVal
��/ 7
)
��7 8
;
��8 9
}
�� 
else
�� 
{
�� 
if
�� 
(
�� 
val
�� 
!=
��  "
null
��# '
&&
��( *
val
��+ .
.
��. /
GetType
��/ 6
(
��6 7
)
��7 8
!=
��9 ;
argField
��< D
.
��D E
PropertyType
��E Q
)
��Q R
val
�� 
=
��  !
ExpressionUtil
��" 0
.
��0 1

ChangeType
��1 ;
(
��; <
val
��< ?
,
��? @
argField
��A I
.
��I J
PropertyType
��J V
)
��V W
;
��W X
propVals
��  
.
��  !
Add
��! $
(
��$ %
argField
��% -
,
��- .
val
��/ 2
)
��2 3
;
��3 4
}
�� 
}
�� 
foreach
�� 
(
�� 
var
�� 
argField
�� %
in
��& (
argType
��) 0
.
��0 1
	GetFields
��1 :
(
��: ;
)
��; <
)
��< =
{
�� 
var
�� 
val
�� 
=
�� %
BuildArgumentFromMember
�� 5
(
��5 6
args
��6 :
,
��: ;
field
��< A
,
��A B
argField
��C K
.
��K L
Name
��L P
,
��P Q
argField
��R Z
.
��Z [
	FieldType
��[ d
,
��d e
argField
��f n
.
��n o
GetValue
��o w
(
��w x
field
��x }
.
��} ~"
ArgumentTypesObject��~ �
)��� �
)��� �
;��� �
	fieldVals
�� 
.
�� 
Add
�� !
(
��! "
argField
��" *
,
��* +
val
��, /
)
��/ 0
;
��0 1
}
�� 
var
�� 
con
�� 
=
�� 
argType
�� !
.
��! "
GetConstructor
��" 0
(
��0 1
propVals
��1 9
.
��9 :
Keys
��: >
.
��> ?
Select
��? E
(
��E F
v
��F G
=>
��H J
v
��K L
.
��L M
PropertyType
��M Y
)
��Y Z
.
��Z [
ToArray
��[ b
(
��b c
)
��c d
)
��d e
;
��e f
object
�� 

parameters
�� !
;
��! "
if
�� 
(
�� 
con
�� 
!=
�� 
null
�� 
)
��  
{
�� 

parameters
�� 
=
��  
con
��! $
.
��$ %
Invoke
��% +
(
��+ ,
propVals
��, 4
.
��4 5
Values
��5 ;
.
��; <
ToArray
��< C
(
��C D
)
��D E
)
��E F
;
��F G
foreach
�� 
(
�� 
var
��  
item
��! %
in
��& (
	fieldVals
��) 2
)
��2 3
{
�� 
item
�� 
.
�� 
Key
��  
.
��  !
SetValue
��! )
(
��) *

parameters
��* 4
,
��4 5
item
��6 :
.
��: ;
Value
��; @
)
��@ A
;
��A B
}
�� 
}
�� 
else
�� 
{
�� 
con
�� 
=
�� 
argType
�� !
.
��! "
GetConstructor
��" 0
(
��0 1
new
��1 4
Type
��5 9
[
��9 :
$num
��: ;
]
��; <
)
��< =
;
��= >

parameters
�� 
=
��  
con
��! $
.
��$ %
Invoke
��% +
(
��+ ,
new
��, /
object
��0 6
[
��6 7
$num
��7 8
]
��8 9
)
��9 :
;
��: ;
foreach
�� 
(
�� 
var
��  
item
��! %
in
��& (
	fieldVals
��) 2
)
��2 3
{
�� 
item
�� 
.
�� 
Key
��  
.
��  !
SetValue
��! )
(
��) *

parameters
��* 4
,
��4 5
item
��6 :
.
��: ;
Value
��; @
)
��@ A
;
��A B
}
�� 
foreach
�� 
(
�� 
var
��  
item
��! %
in
��& (
propVals
��) 1
)
��1 2
{
�� 
item
�� 
.
�� 
Key
��  
.
��  !
SetValue
��! )
(
��) *

parameters
��* 4
,
��4 5
item
��6 :
.
��: ;
Value
��; @
)
��@ A
;
��A B
}
�� 
}
�� 
var
�� 
argParam
�� 
=
�� 

Expression
�� )
.
��) *
	Parameter
��* 3
(
��3 4
argType
��4 ;
)
��; <
;
��< =
result
�� 
.
�� 

Expression
�� !
=
��" #
new
��$ '
ParameterReplacer
��( 9
(
��9 :
)
��: ;
.
��; <
ReplaceByType
��< I
(
��I J
result
��J P
.
��P Q

Expression
��Q [
,
��[ \
argType
��] d
,
��d e
argParam
��f n
)
��n o
;
��o p
result
�� 
.
�� "
AddConstantParameter
�� +
(
��+ ,
argParam
��, 4
,
��4 5

parameters
��6 @
)
��@ A
;
��A B
}
�� 
var
�� 
paramExp
�� 
=
�� 
field
��  
.
��  !

FieldParam
��! +
;
��+ ,
result
�� 
.
�� 

Expression
�� 
=
�� 
new
��  #
ParameterReplacer
��$ 5
(
��5 6
)
��6 7
.
��7 8
Replace
��8 ?
(
��? @
result
��@ F
.
��F G

Expression
��G Q
,
��Q R
paramExp
��S [
,
��[ \
context
��] d
)
��d e
;
��e f
return
�� 
result
�� 
;
�� 
}
�� 	
private
�� 
static
�� 
object
�� %
BuildArgumentFromMember
�� 5
(
��5 6

Dictionary
��6 @
<
��@ A
string
��A G
,
��G H
ExpressionResult
��I Y
>
��Y Z
args
��[ _
,
��_ `
Field
��a f
field
��g l
,
��l m
string
��n t

memberName
��u 
,�� �
Type��� �

memberType��� �
,��� �
object��� �
defaultValue��� �
)��� �
{
�� 	
string
�� 
argName
�� 
=
�� 

memberName
�� '
;
��' (
if
�� 
(
�� 

memberType
�� 
.
�� !
GetGenericArguments
�� .
(
��. /
)
��/ 0
.
��0 1
Any
��1 4
(
��4 5
)
��5 6
&&
��7 9

memberType
��: D
.
��D E&
GetGenericTypeDefinition
��E ]
(
��] ^
)
��^ _
==
��` b
typeof
��c i
(
��i j
RequiredField
��j w
<
��w x
>
��x y
)
��y z
)
��z {
{
�� 
if
�� 
(
�� 
args
�� 
==
�� 
null
��  
||
��! #
!
��$ %
args
��% )
.
��) *
ContainsKey
��* 5
(
��5 6
argName
��6 =
)
��= >
)
��> ?
{
�� 
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� <
(
��< =
$"
��= ?
Field '
��? F
{
��F G
field
��G L
.
��L M
Name
��M Q
}
��Q R+
' missing required argument '
��R o
{
��o p
argName
��p w
}
��w x
'
��x y
"
��y z
)
��z {
;
��{ |
}
�� 
var
�� 
item
�� 
=
�� 

Expression
�� %
.
��% &
Lambda
��& ,
(
��, -
args
��- 1
[
��1 2
argName
��2 9
]
��9 :
)
��: ;
.
��; <
Compile
��< C
(
��C D
)
��D E
.
��E F
DynamicInvoke
��F S
(
��S T
)
��T U
;
��U V
var
�� 
constructor
�� 
=
��  !

memberType
��" ,
.
��, -
GetConstructor
��- ;
(
��; <
new
��< ?
[
��@ A
]
��A B
{
��C D
item
��D H
.
��H I
GetType
��I P
(
��P Q
)
��Q R
}
��R S
)
��S T
;
��T U
if
�� 
(
�� 
constructor
�� 
==
��  "
null
��# '
)
��' (
{
�� 
foreach
�� 
(
�� 
var
��  
c
��! "
in
��# %

memberType
��& 0
.
��0 1
GetConstructors
��1 @
(
��@ A
)
��A B
)
��B C
{
�� 
var
�� 

parameters
�� &
=
��' (
c
��) *
.
��* +
GetParameters
��+ 8
(
��8 9
)
��9 :
;
��: ;
if
�� 
(
�� 

parameters
�� &
.
��& '
Count
��' ,
(
��, -
)
��- .
==
��/ 1
$num
��2 3
)
��3 4
{
�� 
item
��  
=
��! "
ExpressionUtil
��# 1
.
��1 2

ChangeType
��2 <
(
��< =
item
��= A
,
��A B

parameters
��C M
[
��M N
$num
��N O
]
��O P
.
��P Q
ParameterType
��Q ^
)
��^ _
;
��_ `
constructor
�� '
=
��( )

memberType
��* 4
.
��4 5
GetConstructor
��5 C
(
��C D
new
��D G
[
��H I
]
��I J
{
��K L
item
��L P
.
��P Q
GetType
��Q X
(
��X Y
)
��Y Z
}
��Z [
)
��[ \
;
��\ ]
break
�� !
;
��! "
}
�� 
}
�� 
}
�� 
if
�� 
(
�� 
constructor
�� 
==
��  "
null
��# '
)
��' (
{
�� 
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� <
(
��< =
$"
��= ?4
&Could not find a constructor for type 
��? e
{
��e f

memberType
��f p
.
��p q
Name
��q u
}
��u v"
 that takes value '��v �
{��� �
item��� �
}��� �
'��� �
"��� �
)��� �
;��� �
}
�� 
var
�� 
typedVal
�� 
=
�� 
constructor
�� *
.
��* +
Invoke
��+ 1
(
��1 2
new
��2 5
[
��6 7
]
��7 8
{
��9 :
item
��: >
}
��> ?
)
��? @
;
��@ A
return
�� 
typedVal
�� 
;
��  
}
�� 
else
�� 
if
�� 
(
�� 
defaultValue
�� !
!=
��" $
null
��% )
&&
��* ,
defaultValue
��- 9
.
��9 :
GetType
��: A
(
��A B
)
��B C
.
��C D&
IsConstructedGenericType
��D \
&&
��] _
defaultValue
��` l
.
��l m
GetType
��m t
(
��t u
)
��u v
.
��v w'
GetGenericTypeDefinition��w �
(��� �
)��� �
==��� �
typeof��� �
(��� �
EntityQueryType��� �
<��� �
>��� �
)��� �
)��� �
{
�� 
return
�� 
args
�� 
!=
�� 
null
�� #
?
��$ %
args
��& *
[
��* +
argName
��+ 2
]
��2 3
:
��4 5
null
��6 :
;
��: ;
}
�� 
else
�� 
if
�� 
(
�� 
args
�� 
!=
�� 
null
�� !
&&
��" $
args
��% )
.
��) *
ContainsKey
��* 5
(
��5 6
argName
��6 =
)
��= >
)
��> ?
{
�� 
return
�� 

Expression
�� !
.
��! "
Lambda
��" (
(
��( )
args
��) -
[
��- .
argName
��. 5
]
��5 6
)
��6 7
.
��7 8
Compile
��8 ?
(
��? @
)
��@ A
.
��A B
DynamicInvoke
��B O
(
��O P
)
��P Q
;
��Q R
}
�� 
else
�� 
{
�� 
return
�� 
defaultValue
�� #
;
��# $
}
�� 
}
�� 	
public
�� 
string
�� *
GetSchemaTypeNameForRealType
�� 2
(
��2 3
Type
��3 7
type
��8 <
)
��< =
{
�� 	
if
�� 
(
�� 
type
�� 
.
�� 
GetTypeInfo
��  
(
��  !
)
��! "
.
��" #
BaseType
��# +
==
��, .
typeof
��/ 5
(
��5 6
LambdaExpression
��6 F
)
��F G
)
��G H
{
�� 
type
�� 
=
�� 
type
�� 
.
�� !
GetGenericArguments
�� /
(
��/ 0
)
��0 1
[
��1 2
$num
��2 3
]
��3 4
.
��4 5!
GetGenericArguments
��5 H
(
��H I
)
��I J
[
��J K
$num
��K L
]
��L M
;
��M N
if
�� 
(
�� 
type
�� 
.
�� !
IsEnumerableOrArray
�� ,
(
��, -
)
��- .
)
��. /
{
�� 
type
�� 
=
�� 
type
�� 
.
��  !
GetGenericArguments
��  3
(
��3 4
)
��4 5
[
��5 6
$num
��6 7
]
��7 8
;
��8 9
}
�� 
}
�� 
if
�� 
(
�� 
type
�� 
==
�� 
_types
�� 
[
�� 
_queryContextName
�� 0
]
��0 1
.
��1 2
ContextType
��2 =
)
��= >
return
�� 
type
�� 
.
�� 
Name
��  
;
��  !
foreach
�� 
(
�� 
var
�� 
eType
�� 
in
�� !
_types
��" (
.
��( )
Values
��) /
)
��/ 0
{
�� 
if
�� 
(
�� 
eType
�� 
.
�� 
ContextType
�� %
==
��& (
type
��) -
)
��- .
return
�� 
eType
��  
.
��  !
Name
��! %
;
��% &
}
�� 
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 4
(
��4 5
$"
��5 7/
!No mapped entity found for type '
��7 X
{
��X Y
type
��Y ]
}
��] ^
'
��^ _
"
��_ `
)
��` a
;
��a b
}
�� 	
private
�� 
List
�� 
<
�� 
Field
�� 
>
�� 
BuildFields
�� '
(
��' (
object
��( .
	fieldsObj
��/ 8
)
��8 9
{
�� 	
var
�� 
	fieldList
�� 
=
�� 
new
�� 
List
��  $
<
��$ %
Field
��% *
>
��* +
(
��+ ,
)
��, -
;
��- .
foreach
�� 
(
�� 
var
�� 
prop
�� 
in
��  
	fieldsObj
��! *
.
��* +
GetType
��+ 2
(
��2 3
)
��3 4
.
��4 5
GetProperties
��5 B
(
��B C
)
��C D
)
��D E
{
�� 
var
�� 
field
�� 
=
�� 
prop
��  
.
��  !
GetValue
��! )
(
��) *
	fieldsObj
��* 3
)
��3 4
as
��5 7
Field
��8 =
;
��= >
field
�� 
.
�� 
Name
�� 
=
�� 
prop
�� !
.
��! "
Name
��" &
;
��& '
	fieldList
�� 
.
�� 
Add
�� 
(
�� 
field
�� #
)
��# $
;
��$ %
}
�� 
return
�� 
	fieldList
�� 
;
�� 
}
�� 	
public
�� 
bool
�� 
HasType
�� 
(
�� 
string
�� "
typeName
��# +
)
��+ ,
{
�� 	
return
�� 
_types
�� 
.
�� 
ContainsKey
�� %
(
��% &
typeName
��& .
)
��. /
;
��/ 0
}
�� 	
public
�� 
bool
�� 
HasType
�� 
(
�� 
Type
��  
type
��! %
)
��% &
{
�� 	
if
�� 
(
�� 
type
�� 
==
�� 
_types
�� 
[
�� 
_queryContextName
�� 0
]
��0 1
.
��1 2
ContextType
��2 =
)
��= >
return
�� 
true
�� 
;
�� 
foreach
�� 
(
�� 
var
�� 
eType
�� 
in
�� !
_types
��" (
.
��( )
Values
��) /
)
��/ 0
{
�� 
if
�� 
(
�� 
eType
�� 
.
�� 
ContextType
�� %
==
��& (
type
��) -
)
��- .
return
�� 
true
�� 
;
��  
}
�� 
return
�� 
false
�� 
;
�� 
}
�� 	
public
�� 
string
�� 
GetGraphQLSchema
�� &
(
��& '
)
��' (
{
�� 	
var
�� 
extraMappings
�� 
=
�� !
_customTypeMappings
��  3
.
��3 4
ToDictionary
��4 @
(
��@ A
k
��A B
=>
��C E
k
��F G
.
��G H
Key
��H K
,
��K L
v
��M N
=>
��O Q
v
��R S
.
��S T
Value
��T Y
)
��Y Z
;
��Z [
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��  #
_customScalarMappings
��! 6
)
��6 7
{
�� 
extraMappings
�� 
[
�� 
item
�� "
.
��" #
Key
��# &
]
��& '
=
��( )
item
��* .
.
��. /
Value
��/ 4
;
��4 5
}
�� 
return
�� 
SchemaGenerator
�� "
.
��" #
Make
��# '
(
��' (
this
��( ,
,
��, -
extraMappings
��. ;
,
��; <
this
��= A
.
��A B#
_customScalarMappings
��B W
)
��W X
;
��X Y
}
�� 	
public
�� 
void
�� !
AddCustomScalarType
�� '
(
��' (
Type
��( ,
clrType
��- 4
,
��4 5
string
��6 <
gqlTypeName
��= H
)
��H I
{
�� 	
this
�� 
.
�� #
_customScalarMappings
�� &
.
��& '
Add
��' *
(
��* +
clrType
��+ 2
,
��2 3
gqlTypeName
��4 ?
)
��? @
;
��@ A-
SetupIntrospectionTypesAndField
�� +
(
��+ ,
)
��, -
;
��- .
}
�� 	
public
�� 
IEnumerable
�� 
<
�� 
Field
��  
>
��  !
GetQueryFields
��" 0
(
��0 1
)
��1 2
{
�� 	
return
�� 
_types
�� 
[
�� 
_queryContextName
�� +
]
��+ ,
.
��, -
	GetFields
��- 6
(
��6 7
)
��7 8
;
��8 9
}
�� 	
public
�� 
IEnumerable
�� 
<
�� 
ISchemaType
�� &
>
��& ' 
GetNonContextTypes
��( :
(
��: ;
)
��; <
{
�� 	
return
�� 
_types
�� 
.
�� 
Values
��  
.
��  !
Where
��! &
(
��& '
s
��' (
=>
��) +
s
��, -
.
��- .
Name
��. 2
!=
��3 5
_queryContextName
��6 G
)
��G H
.
��H I
ToList
��I O
(
��O P
)
��P Q
;
��Q R
}
�� 	
public
�� 
IEnumerable
�� 
<
�� 
IMethodType
�� &
>
��& '
GetMutations
��( 4
(
��4 5
)
��5 6
{
�� 	
return
�� 

_mutations
�� 
.
�� 
Values
�� $
.
��$ %
ToList
��% +
(
��+ ,
)
��, -
;
��- .
}
�� 	
public
�� 
void
�� $
RemoveTypeAndAllFields
�� *
<
��* +
TSchemaType
��+ 6
>
��6 7
(
��7 8
)
��8 9
{
�� 	
this
�� 
.
�� $
RemoveTypeAndAllFields
�� '
(
��' (
typeof
��( .
(
��. /
TSchemaType
��/ :
)
��: ;
.
��; <
Name
��< @
)
��@ A
;
��A B
}
�� 	
public
�� 
void
�� $
RemoveTypeAndAllFields
�� *
(
��* +
string
��+ 1
typeName
��2 :
)
��: ;
{
�� 	
foreach
�� 
(
�� 
var
�� 
context
��  
in
��! #
_types
��$ *
.
��* +
Values
��+ 1
)
��1 2
{
��  
RemoveFieldsOfType
�� "
(
��" #
typeName
��# +
,
��+ ,
context
��- 4
)
��4 5
;
��5 6
}
�� 
_types
�� 
.
�� 
Remove
�� 
(
�� 
typeName
�� "
)
��" #
;
��# $
}
�� 	
private
�� 
void
��  
RemoveFieldsOfType
�� '
(
��' (
string
��( .
typeName
��/ 7
,
��7 8
ISchemaType
��9 D
contextType
��E P
)
��P Q
{
�� 	
foreach
�� 
(
�� 
var
�� 
field
�� 
in
�� !
contextType
��" -
.
��- .
	GetFields
��. 7
(
��7 8
)
��8 9
.
��9 :
ToList
��: @
(
��@ A
)
��A B
)
��B C
{
�� 
if
�� 
(
�� 
field
�� 
.
�� 
ReturnTypeSingle
�� *
==
��+ -
typeName
��. 6
)
��6 7
{
�� 
contextType
�� 
.
��  
RemoveField
��  +
(
��+ ,
field
��, 1
.
��1 2
Name
��2 6
)
��6 7
;
��7 8
}
�� 
}
�� 
}
�� 	
}
�� 
}�� �7
IY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\Models\Introspection.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
. 
Models %
{ 
public 

partial 
class 
Schema 
{ 
public		 
TypeElement		 
	QueryType		 $
{		% &
get		' *
;		* +
set		, /
;		/ 0
}		1 2
public 
TypeElement 
MutationType '
{( )
get* -
;- .
set/ 2
;2 3
}4 5
public 
SubscriptionType 
SubscriptionType  0
{1 2
get3 6
;6 7
set8 ;
;; <
}= >
public 
TypeElement 
[ 
] 
Types "
{# $
get% (
;( )
set* -
;- .
}/ 0
public 

Directives 
[ 
] 

Directives &
{' (
get) ,
;, -
set. 1
;1 2
}3 4
} 
public 

partial 
class 
SubscriptionType )
{ 
public 
string 
Name 
{ 
get  
;  !
set" %
;% &
}' (
} 
public 

partial 
class 
TypeElement $
{ 
public 
TypeElement 
( 
) 
{ 	

EnumValues 
= 
new 
Models #
.# $
	EnumValue$ -
[- .
]. /
{0 1
}1 2
;2 3

Interfaces 
= 
new 
TypeElement (
[( )
]) *
{+ ,
}, -
;- .
PossibleTypes 
= 
new 
TypeElement  +
[+ ,
], -
{. /
}/ 0
;0 1
InputFields   
=   
new   

InputValue   (
[  ( )
]  ) *
{  + ,
}  , -
;  - .
}!! 	
public## 
string## 
Kind## 
{## 
get##  
;##  !
set##" %
;##% &
}##' (
public%% 
string%% 
Name%% 
{%% 
get%%  
;%%  !
set%%" %
;%%% &
}%%' (
public'' 
string'' 
Description'' !
{''" #
get''$ '
;''' (
set'') ,
;'', -
}''. /
public++ 

InputValue++ 
[++ 
]++ 
InputFields++ '
{++( )
get++* -
;++- .
set++/ 2
;++2 3
}++4 5
public-- 
TypeElement-- 
[-- 
]-- 

Interfaces-- '
{--( )
get--* -
;--- .
set--/ 2
;--2 3
}--4 5
public// 
	EnumValue// 
[// 
]// 

EnumValues// %
{//& '
get//( +
;//+ ,
set//- 0
;//0 1
}//2 3
public11 
TypeElement11 
[11 
]11 
PossibleTypes11 *
{11+ ,
get11- 0
;110 1
set112 5
;115 6
}117 8
public22 
TypeElement22 
OfType22 !
{22" #
get22$ '
;22' (
set22) ,
;22, -
}22. /
}33 
public55 

partial55 
class55 
Field55 
{66 
public77 
string77 
Name77 
{77 
get77  
;77  !
set77" %
;77% &
}77' (
public99 
string99 
Description99 !
{99" #
get99$ '
;99' (
set99) ,
;99, -
}99. /
public;; 

InputValue;; 
[;; 
];; 
Args;;  
{;;! "
get;;# &
;;;& '
set;;( +
;;;+ ,
};;- .
public== 
TypeElement== 
Type== 
{==  !
get==" %
;==% &
set==' *
;==* +
}==, -
public?? 
bool?? 
IsDeprecated??  
{??! "
get??# &
;??& '
set??( +
;??+ ,
}??- .
publicAA 
stringAA 
DeprecationReasonAA '
{AA( )
getAA* -
;AA- .
setAA/ 2
;AA2 3
}AA4 5
}BB 
publicDD 

classDD 

InputValueDD 
{EE 
publicFF 
stringFF 
NameFF 
{FF 
getFF  
;FF  !
setFF" %
;FF% &
}FF' (
publicGG 
stringGG 
DescriptionGG !
{GG" #
getGG$ '
;GG' (
setGG) ,
;GG, -
}GG. /
publicHH 
TypeElementHH 
TypeHH 
{HH  !
getHH" %
;HH% &
setHH' *
;HH* +
}HH, -
publicII 
objectII 
DefaultValueII "
{II# $
getII% (
;II( )
setII* -
;II- .
}II/ 0
}JJ 
publicLL 

partialLL 
classLL 

DirectivesLL #
{MM 
publicNN 
stringNN 
NameNN 
{NN 
getNN  
;NN  !
setNN" %
;NN% &
}NN' (
publicPP 
objectPP 
DescriptionPP !
{PP" #
getPP$ '
;PP' (
setPP) ,
;PP, -
}PP. /
publicRR 
stringRR 
[RR 
]RR 
	LocationsRR !
{RR" #
getRR$ '
;RR' (
setRR) ,
;RR, -
}RR. /
publicTT 

InputValueTT 
[TT 
]TT 
ArgsTT  
{TT! "
getTT# &
;TT& '
setTT( +
;TT+ ,
}TT- .
}UU 
publicWW 

partialWW 
classWW 
	EnumValueWW "
{XX 
publicYY 
stringYY 
NameYY 
{YY 
getYY  
;YY  !
setYY" %
;YY% &
}YY' (
public[[ 
string[[ 
Description[[ !
{[[" #
get[[$ '
;[[' (
set[[) ,
;[[, -
}[[. /
public]] 
bool]] 
IsDeprecated]]  
{]]! "
get]]# &
;]]& '
set]]( +
;]]+ ,
}]]- .
public__ 
string__ 
DeprecationReason__ '
{__( )
get__* -
;__- .
set__/ 2
;__2 3
}__4 5
}`` 
}aa ��
AY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\MutationType.cs
	namespace

 	
EntityGraphQL


 
.

 
Schema

 
{ 
public 

class 
MutationType 
: 
IMethodType  +
{ 
private 
readonly 
ISchemaType $

returnType% /
;/ 0
private 
readonly 
object !
mutationClassInstance  5
;5 6
private 
readonly 

MethodInfo #
method$ *
;* +
private 

Dictionary 
< 
string !
,! "
Type# '
>' (
argumentTypes) 6
=7 8
new9 <

Dictionary= G
<G H
stringH N
,N O
TypeP T
>T U
(U V
)V W
;W X
private 
readonly 
Type 
argInstanceType -
;- .
public 
Type 
ReturnTypeClr !
{" #
get$ '
{( )
return* 0

returnType1 ;
.; <
ContextType< G
;G H
}I J
}K L
public 
string 
Description !
{" #
get$ '
;' (
}) *
public 
object 
Call 
( 
object !
[! "
]" #
args$ (
,( )

Dictionary* 4
<4 5
string5 ;
,; <
ExpressionResult= M
>M N
gqlRequestArgsO ]
)] ^
{ 	
var 
allArgs 
= 
new 
List "
<" #
object# )
>) *
{+ ,
args- 1
.1 2
First2 7
(7 8
)8 9
}: ;
;; <
var 
parameterInfo 
= 
method  &
.& '
GetParameters' 4
(4 5
)5 6
;6 7
foreach 
( 
var 
p 
in 
parameterInfo +
.+ ,
Skip, 0
(0 1
$num1 2
)2 3
.3 4
Take4 8
(8 9
parameterInfo9 F
.F G
LengthG M
-N O
$numP Q
)Q R
)R S
{   
var!! 
match!! 
=!! 
args!!  
.!!  !
FirstOrDefault!!! /
(!!/ 0
a!!0 1
=>!!2 4
p!!5 6
.!!6 7
ParameterType!!7 D
.!!D E
IsAssignableFrom!!E U
(!!U V
a!!V W
.!!W X
GetType!!X _
(!!_ `
)!!` a
)!!a b
)!!b c
;!!c d
if"" 
("" 
match"" 
=="" 
null"" !
)""! "
{## 
throw$$ 
new$$ *
EntityGraphQLCompilerException$$ <
($$< =
$"$$= ?
	Mutation $$? H
{$$H I
method$$I O
.$$O P
Name$$P T
}$$T U!
 expecting parameter $$U j
{$$j k
p$$k l
.$$l m
Name$$m q
}$$q r
	 of type $$r {
{$${ |
p$$| }
.$$} ~
ParameterType	$$~ �
}
$$� �M
?, but no arguments suuplied to GraphQL QueryObject of that type
$$� �
"
$$� �
)
$$� �
;
$$� �
}%% 
allArgs&& 
.&& 
Add&& 
(&& 
match&& !
)&&! "
;&&" #
}'' 
var** 
argInstance** 
=** 
AssignArgValues** -
(**- .
gqlRequestArgs**. <
)**< =
;**= >
allArgs++ 
.++ 
Add++ 
(++ 
argInstance++ #
)++# $
;++$ %
var-- 
result-- 
=-- 
method-- 
.--  
Invoke--  &
(--& '!
mutationClassInstance--' <
,--< =
allArgs--> E
.--E F
ToArray--F M
(--M N
)--N O
)--O P
;--P Q
return.. 
result.. 
;.. 
}// 	
private11 
object11 
AssignArgValues11 &
(11& '

Dictionary11' 1
<111 2
string112 8
,118 9
ExpressionResult11: J
>11J K
gqlRequestArgs11L Z
)11Z [
{22 	
var33 
argInstance33 
=33 
	Activator33 '
.33' (
CreateInstance33( 6
(336 7
this337 ;
.33; <
argInstanceType33< K
)33K L
;33L M
Type44 
argType44 
=44 
this44 
.44  
argInstanceType44  /
;44/ 0
foreach55 
(55 
var55 
key55 
in55 
gqlRequestArgs55  .
.55. /
Keys55/ 3
)553 4
{66 
var77 
	foundProp77 
=77 
false77  %
;77% &
foreach88 
(88 
var88 
prop88 !
in88" $
argType88% ,
.88, -
GetProperties88- :
(88: ;
)88; <
)88< =
{99 
var:: 
propName::  
=::! "
SchemaGenerator::# 2
.::2 3"
ToCamelCaseStartsLower::3 I
(::I J
prop::J N
.::N O
Name::O S
)::S T
;::T U
if;; 
(;; 
key;; 
==;; 
propName;; '
);;' (
{<< 
object== 
value== $
===% &
GetValue==' /
(==/ 0
gqlRequestArgs==0 >
,==> ?
propName==@ H
,==H I
prop==J N
.==N O
PropertyType==O [
)==[ \
;==\ ]
prop>> 
.>> 
SetValue>> %
(>>% &
argInstance>>& 1
,>>1 2
value>>3 8
)>>8 9
;>>9 :
	foundProp?? !
=??" #
true??$ (
;??( )
}@@ 
}AA 
ifBB 
(BB 
!BB 
	foundPropBB 
)BB 
{CC 
foreachDD 
(DD 
varDD  
fieldDD! &
inDD' )
argTypeDD* 1
.DD1 2
	GetFieldsDD2 ;
(DD; <
)DD< =
)DD= >
{EE 
varFF 
	fieldNameFF %
=FF& '
SchemaGeneratorFF( 7
.FF7 8"
ToCamelCaseStartsLowerFF8 N
(FFN O
fieldFFO T
.FFT U
NameFFU Y
)FFY Z
;FFZ [
ifGG 
(GG 
keyGG 
==GG  "
	fieldNameGG# ,
)GG, -
{HH 
objectII "
valueII# (
=II) *
GetValueII+ 3
(II3 4
gqlRequestArgsII4 B
,IIB C
	fieldNameIID M
,IIM N
fieldIIO T
.IIT U
	FieldTypeIIU ^
)II^ _
;II_ `
fieldJJ !
.JJ! "
SetValueJJ" *
(JJ* +
argInstanceJJ+ 6
,JJ6 7
valueJJ8 =
)JJ= >
;JJ> ?
	foundPropKK %
=KK& '
trueKK( ,
;KK, -
}LL 
}MM 
}NN 
ifOO 
(OO 
!OO 
	foundPropOO 
)OO 
{PP 
throwQQ 
newQQ "
EntityQuerySchemaErrorQQ 4
(QQ4 5
$"QQ5 7-
!Could not find property or field QQ7 X
{QQX Y
keyQQY \
}QQ\ ]
 on schema object QQ] o
{QQo p
argTypeQQp w
.QQw x
NameQQx |
}QQ| }
"QQ} ~
)QQ~ 
;	QQ �
}RR 
}SS 
returnTT 
argInstanceTT 
;TT 
}UU 	
private]] 
static]] 
List]] 
<]] 
T]] 
>]] 
ConvertArray]] +
<]]+ ,
T]], -
>]]- .
(]]. /
Array]]/ 4
input]]5 :
)]]: ;
{^^ 	
return__ 
input__ 
.__ 
Cast__ 
<__ 
T__ 
>__  
(__  !
)__! "
.__" #
ToList__# )
(__) *
)__* +
;__+ ,
}`` 	
privatebb 
objectbb 
GetValuebb 
(bb  

Dictionarybb  *
<bb* +
stringbb+ 1
,bb1 2
ExpressionResultbb3 C
>bbC D
gqlRequestArgsbbE S
,bbS T
stringbbU [

memberNamebb\ f
,bbf g
Typebbh l

memberTypebbm w
)bbw x
{cc 	
objectdd 
valuedd 
=dd 

Expressiondd %
.dd% &
Lambdadd& ,
(dd, -
gqlRequestArgsdd- ;
[dd; <

memberNamedd< F
]ddF G
)ddG H
.ddH I
CompileddI P
(ddP Q
)ddQ R
.ddR S
DynamicInvokeddS `
(dd` a
)dda b
;ddb c
ifee 
(ee 
valueee 
!=ee 
nullee 
)ee 
{ff 
Typegg 
typegg 
=gg 
valuegg !
.gg! "
GetTypegg" )
(gg) *
)gg* +
;gg+ ,
ifhh 
(hh 
typehh 
.hh 
IsArrayhh  
&&hh! #

memberTypehh$ .
.hh. /
IsEnumerableOrArrayhh/ B
(hhB C
)hhC D
)hhD E
{ii 
varjj 
arrjj 
=jj 
(jj 
Arrayjj $
)jj$ %
valuejj% *
;jj* +
varkk 
convertMethodkk %
=kk& '
typeofkk( .
(kk. /
MutationTypekk/ ;
)kk; <
.kk< =
	GetMethodkk= F
(kkF G
$strkkG U
,kkU V
BindingFlagskkW c
.kkc d
	NonPublickkd m
|kkn o
BindingFlagskkp |
.kk| }
Static	kk} �
)
kk� �
;
kk� �
varll 
genericll 
=ll  !
convertMethodll" /
.ll/ 0
MakeGenericMethodll0 A
(llA B
newllB E
[llE F
]llF G
{llH I

memberTypellI S
.llS T
GetGenericArgumentsllT g
(llg h
)llh i
[lli j
$numllj k
]llk l
}lll m
)llm n
;lln o
valuemm 
=mm 
genericmm #
.mm# $
Invokemm$ *
(mm* +
nullmm+ /
,mm/ 0
newmm1 4
objectmm5 ;
[mm; <
]mm< =
{mm> ?
valuemm@ E
}mmF G
)mmG H
;mmH I
}nn 
elseoo 
ifoo 
(oo 
typeoo 
==oo  
typeofoo! '
(oo' (

Newtonsoftoo( 2
.oo2 3
Jsonoo3 7
.oo7 8
Linqoo8 <
.oo< =
JObjectoo= D
)ooD E
)ooE F
{pp 
valueqq 
=qq 
(qq 
(qq 

Newtonsoftqq (
.qq( )
Jsonqq) -
.qq- .
Linqqq. 2
.qq2 3
JObjectqq3 :
)qq: ;
valueqq; @
)qq@ A
.qqA B
ToObjectqqB J
(qqJ K

memberTypeqqK U
)qqU V
;qqV W
}rr 
elsess 
{tt 
valueuu 
=uu 
ExpressionUtiluu *
.uu* +

ChangeTypeuu+ 5
(uu5 6
valueuu6 ;
,uu; <

memberTypeuu= G
)uuG H
;uuH I
}vv 
}ww 
returnxx 
valuexx 
;xx 
}yy 	
public{{ 
Type{{ 
ContextType{{ 
=>{{  "

ReturnType{{# -
.{{- .
ContextType{{. 9
;{{9 :
public}} 
string}} 
Name}} 
{}} 
get}}  
;}}  !
}}}" #
public 
ISchemaType 

ReturnType %
=>& (

returnType) 3
;3 4
public
�� 
bool
�� 
IsEnumerable
��  
=>
��! #

ReturnType
��$ .
.
��. /
ContextType
��/ :
.
��: ;!
IsEnumerableOrArray
��; N
(
��N O
)
��O P
;
��P Q
public
�� 
IDictionary
�� 
<
�� 
string
�� !
,
��! "
Type
��# '
>
��' (
	Arguments
��) 2
=>
��3 5
argumentTypes
��6 C
;
��C D
public
�� 
string
�� 
ReturnTypeSingle
�� &
=>
��' )

returnType
��* 4
.
��4 5
Name
��5 9
;
��9 :
public
�� 
MutationType
�� 
(
�� 
string
�� "

methodName
��# -
,
��- .
ISchemaType
��/ :

returnType
��; E
,
��E F
object
��G M#
mutationClassInstance
��N c
,
��c d

MethodInfo
��e o
method
��p v
,
��v w
string
��x ~
description�� �
)��� �
{
�� 	
this
�� 
.
�� 
Description
�� 
=
�� 
description
�� *
;
��* +
this
�� 
.
�� 

returnType
�� 
=
�� 

returnType
�� (
;
��( )
this
�� 
.
�� #
mutationClassInstance
�� &
=
��' (#
mutationClassInstance
��) >
;
��> ?
this
�� 
.
�� 
method
�� 
=
�� 
method
��  
;
��  !
Name
�� 
=
�� 

methodName
�� 
;
�� 
var
�� 
	methodArg
�� 
=
�� 
method
�� "
.
��" #
GetParameters
��# 0
(
��0 1
)
��1 2
.
��2 3
Last
��3 7
(
��7 8
)
��8 9
;
��9 :
this
�� 
.
�� 
argInstanceType
��  
=
��! "
	methodArg
��# ,
.
��, -
ParameterType
��- :
;
��: ;
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��  
argInstanceType
��! 0
.
��0 1
GetProperties
��1 >
(
��> ?
)
��? @
)
��@ A
{
�� 
argumentTypes
�� 
.
�� 
Add
�� !
(
��! "
SchemaGenerator
��" 1
.
��1 2$
ToCamelCaseStartsLower
��2 H
(
��H I
item
��I M
.
��M N
Name
��N R
)
��R S
,
��S T
item
��U Y
.
��Y Z
PropertyType
��Z f
)
��f g
;
��g h
}
�� 
foreach
�� 
(
�� 
var
�� 
item
�� 
in
��  
argInstanceType
��! 0
.
��0 1
	GetFields
��1 :
(
��: ;
)
��; <
)
��< =
{
�� 
argumentTypes
�� 
.
�� 
Add
�� !
(
��! "
SchemaGenerator
��" 1
.
��1 2$
ToCamelCaseStartsLower
��2 H
(
��H I
item
��I M
.
��M N
Name
��N R
)
��R S
,
��S T
item
��U Y
.
��Y Z
	FieldType
��Z c
)
��c d
;
��d e
}
�� 
}
�� 	
public
�� 
Field
�� 
GetField
�� 
(
�� 
string
�� $

identifier
��% /
)
��/ 0
{
�� 	
return
�� 

ReturnType
�� 
.
�� 
GetField
�� &
(
��& '

identifier
��' 1
)
��1 2
;
��2 3
}
�� 	
public
�� 
bool
�� 
HasArgumentByName
�� %
(
��% &
string
��& ,
argName
��- 4
)
��4 5
{
�� 	
return
�� 
argumentTypes
��  
.
��  !
ContainsKey
��! ,
(
��, -
argName
��- 4
)
��4 5
;
��5 6
}
�� 	
public
�� 
Type
�� 
GetArgumentType
�� #
(
��# $
string
��$ *
argName
��+ 2
)
��2 3
{
�� 	
if
�� 
(
�� 
!
�� 
argumentTypes
�� 
.
�� 
ContainsKey
�� *
(
��* +
argName
��+ 2
)
��2 3
)
��3 4
{
�� 
throw
�� 
new
�� $
EntityQuerySchemaError
�� 0
(
��0 1
$"
��1 34
&Argument type not found for argument '
��3 Y
{
��Y Z
argName
��Z a
}
��a b
'
��b c
"
��c d
)
��d e
;
��e f
}
�� 
return
�� 
argumentTypes
��  
[
��  !
argName
��! (
]
��( )
;
��) *
}
�� 	
}
�� 
}�� ��
BY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\SchemaBuilder.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public 

class 
SchemaBuilder 
{ 
private 
static 
readonly 
HashSet  '
<' (
string( .
>. /
ignoreProps0 ;
=< =
new> A
HashSetB I
<I J
stringJ P
>P Q
{R S
$str 
, 
$str 
, 
$str 
} 	
;	 

private 
static 
readonly 
HashSet  '
<' (
string( .
>. /
ignoreTypes0 ;
=< =
new> A
HashSetB I
<I J
stringJ P
>P Q
{R S
$str 
, 
$str 
} 	
;	 

public$$ 
static$$  
MappedSchemaProvider$$ *
<$$* +
TContextType$$+ 7
>$$7 8

FromObject$$9 C
<$$C D
TContextType$$D P
>$$P Q
($$Q R
bool$$R V!
autoCreateIdArguments$$W l
=$$m n
true$$o s
)$$s t
{%% 	
var&& 
schema&& 
=&& 
new&&  
MappedSchemaProvider&& 1
<&&1 2
TContextType&&2 >
>&&> ?
(&&? @
)&&@ A
;&&A B
var'' 
contextType'' 
='' 
typeof'' $
(''$ %
TContextType''% 1
)''1 2
;''2 3
var(( 

rootFields(( 
=(( '
AddFieldsFromObjectToSchema(( 8
<((8 9
TContextType((9 E
>((E F
(((F G
contextType((G R
,((R S
schema((T Z
)((Z [
;(([ \
foreach)) 
()) 
var)) 
f)) 
in)) 

rootFields)) (
)))( )
{** 
if++ 
(++ !
autoCreateIdArguments++ )
)++) *
{,, *
AddFieldWithIdArgumentIfExists.. 2
(..2 3
schema..3 9
,..9 :
contextType..; F
,..F G
f..H I
)..I J
;..J K
}// 
schema00 
.00 
AddField00 
(00  
f00  !
)00! "
;00" #
}11 
return22 
schema22 
;22 
}33 	
private55 
static55 
void55 *
AddFieldWithIdArgumentIfExists55 :
<55: ;
TContextType55; G
>55G H
(55H I 
MappedSchemaProvider55I ]
<55] ^
TContextType55^ j
>55j k
schema55l r
,55r s
Type55t x
contextType	55y �
,
55� �
Field
55� �
	fieldProp
55� �
)
55� �
{66 	
if77 
(77 
!77 
	fieldProp77 
.77 
Resolve77 "
.77" #
Type77# '
.77' (
IsEnumerableOrArray77( ;
(77; <
)77< =
)77= >
return88 
;88 
var99 

schemaType99 
=99 
schema99 #
.99# $
Type99$ (
(99( )
	fieldProp99) 2
.992 3
ReturnTypeSingle993 C
)99C D
;99D E
var:: 

idFieldDef:: 
=:: 

schemaType:: '
.::' (
	GetFields::( 1
(::1 2
)::2 3
.::3 4
FirstOrDefault::4 B
(::B C
f::C D
=>::E G
f::H I
.::I J
Name::J N
==::O Q
$str::R V
)::V W
;::W X
if;; 
(;; 

idFieldDef;; 
==;; 
null;; "
);;" #
return<< 
;<< 
varAA 
requiredFieldTypeAA !
=AA" #
typeofAA$ *
(AA* +
RequiredFieldAA+ 8
<AA8 9
>AA9 :
)AA: ;
.AA; <
MakeGenericTypeAA< K
(AAK L

idFieldDefAAL V
.AAV W
ResolveAAW ^
.AA^ _
TypeAA_ c
)AAc d
;AAd e
varBB 
fieldNameAndTypeBB  
=BB! "
newBB# &

DictionaryBB' 1
<BB1 2
stringBB2 8
,BB8 9
TypeBB: >
>BB> ?
{BB@ A
{BBB C
$strBBD H
,BBH I
requiredFieldTypeBBJ [
}BB\ ]
}BB^ _
;BB_ `
varCC 
argTypesCC 
=CC "
LinqRuntimeTypeBuilderCC 1
.CC1 2
GetDynamicTypeCC2 @
(CC@ A
fieldNameAndTypeCCA Q
)CCQ R
;CCR S
varDD 
argTypesValueDD 
=DD 
argTypesDD  (
.DD( )
GetTypeInfoDD) 4
(DD4 5
)DD5 6
.DD6 7
GetConstructorsDD7 F
(DDF G
)DDG H
[DDH I
$numDDI J
]DDJ K
.DDK L
InvokeDDL R
(DDR S
newDDS V
TypeDDW [
[DD[ \
$numDD\ ]
]DD] ^
)DD^ _
;DD_ `
varEE 
argTypeParamEE 
=EE 

ExpressionEE )
.EE) *
	ParameterEE* 3
(EE3 4
argTypesEE4 <
)EE< =
;EE= >
TypeFF 
arrayContextTypeFF !
=FF" #
schemaFF$ *
.FF* +
TypeFF+ /
(FF/ 0
	fieldPropFF0 9
.FF9 :
ReturnTypeSingleFF: J
)FFJ K
.FFK L
ContextTypeFFL W
;FFW X
varGG 
arrayContextParamGG !
=GG" #

ExpressionGG$ .
.GG. /
	ParameterGG/ 8
(GG8 9
arrayContextTypeGG9 I
)GGI J
;GGJ K
varHH 
ctxIdHH 
=HH 

ExpressionHH "
.HH" #
PropertyOrFieldHH# 2
(HH2 3
arrayContextParamHH3 D
,HHD E
$strHHF J
)HHJ K
;HHK L

ExpressionII 
argIdII 
=II 

ExpressionII )
.II) *
PropertyOrFieldII* 9
(II9 :
argTypeParamII: F
,IIF G
$strIIH L
)IIL M
;IIM N
argIdJJ 
=JJ 

ExpressionJJ 
.JJ 
PropertyJJ '
(JJ' (
argIdJJ( -
,JJ- .
$strJJ/ 6
)JJ6 7
;JJ7 8
varKK 
idBodyKK 
=KK 

ExpressionKK #
.KK# $

MakeBinaryKK$ .
(KK. /
ExpressionTypeKK/ =
.KK= >
EqualKK> C
,KKC D
ctxIdKKE J
,KKJ K
argIdKKL Q
)KKQ R
;KKR S
varLL 
idLambdaLL 
=LL 

ExpressionLL %
.LL% &
LambdaLL& ,
(LL, -
idBodyLL- 3
,LL3 4
newLL5 8
[LL8 9
]LL9 :
{LL; <
arrayContextParamLL= N
}LLO P
)LLP Q
;LLQ R

ExpressionMM 
bodyMM 
=MM 
ExpressionUtilMM ,
.MM, -
MakeExpressionCallMM- ?
(MM? @
newMM@ C
[MMC D
]MMD E
{MMF G
typeofMMH N
(MMN O
	QueryableMMO X
)MMX Y
,MMY Z
typeofMM[ a
(MMa b

EnumerableMMb l
)MMl m
}MMn o
,MMo p
$strMMq x
,MMx y
newMMz }
Type	MM~ �
[
MM� �
]
MM� �
{
MM� �
arrayContextType
MM� �
}
MM� �
,
MM� �
	fieldProp
MM� �
.
MM� �
Resolve
MM� �
,
MM� �
idLambda
MM� �
)
MM� �
;
MM� �
bodyOO 
=OO 
ExpressionUtilOO !
.OO! "
MakeExpressionCallOO" 4
(OO4 5
newOO5 8
[OO8 9
]OO9 :
{OO; <
typeofOO= C
(OOC D
	QueryableOOD M
)OOM N
,OON O
typeofOOP V
(OOV W

EnumerableOOW a
)OOa b
}OOc d
,OOd e
$strOOf v
,OOv w
newOOx {
Type	OO| �
[
OO� �
]
OO� �
{
OO� �
arrayContextType
OO� �
}
OO� �
,
OO� �
body
OO� �
)
OO� �
;
OO� �
varPP 
contextParamPP 
=PP 

ExpressionPP )
.PP) *
	ParameterPP* 3
(PP3 4
contextTypePP4 ?
)PP? @
;PP@ A
varQQ 
lambdaParamsQQ 
=QQ 
newQQ "
[QQ" #
]QQ# $
{QQ% &
contextParamQQ' 3
,QQ3 4
argTypeParamQQ5 A
}QQB C
;QQC D
bodyRR 
=RR 
newRR 
ParameterReplacerRR (
(RR( )
)RR) *
.RR* +
ReplaceByTypeRR+ 8
(RR8 9
bodyRR9 =
,RR= >
contextTypeRR? J
,RRJ K
contextParamRRL X
)RRX Y
;RRY Z
varSS 
selectionExpressionSS #
=SS$ %

ExpressionSS& 0
.SS0 1
LambdaSS1 7
(SS7 8
bodySS8 <
,SS< =
lambdaParamsSS> J
)SSJ K
;SSK L
varTT 
nameTT 
=TT 
	fieldPropTT  
.TT  !
NameTT! %
.TT% &
SingularizeTT& 1
(TT1 2
)TT2 3
;TT3 4
ifUU 
(UU 
nameUU 
==UU 
nullUU 
)UU 
{VV 
nameXX 
=XX 
$"XX 
{XX 
	fieldPropXX #
.XX# $
NameXX$ (
}XX( )
ByIdXX) -
"XX- .
;XX. /
}YY 
varZZ 
fieldZZ 
=ZZ 
newZZ 
FieldZZ !
(ZZ! "
nameZZ" &
,ZZ& '
selectionExpressionZZ( ;
,ZZ; <
$"ZZ= ?
	Return a ZZ? H
{ZZH I
	fieldPropZZI R
.ZZR S
ReturnTypeSingleZZS c
}ZZc d

 by its IdZZd n
"ZZn o
,ZZo p
	fieldPropZZq z
.ZZz {
ReturnTypeSingle	ZZ{ �
,
ZZ� �
argTypesValue
ZZ� �
)
ZZ� �
;
ZZ� �
schema[[ 
.[[ 
AddField[[ 
([[ 
field[[ !
)[[! "
;[[" #
}\\ 	
private^^ 
static^^ 
List^^ 
<^^ 
Field^^ !
>^^! "'
AddFieldsFromObjectToSchema^^# >
<^^> ?
TContextType^^? K
>^^K L
(^^L M
Type^^M Q
type^^R V
,^^V W 
MappedSchemaProvider^^X l
<^^l m
TContextType^^m y
>^^y z
schema	^^{ �
)
^^� �
{__ 	
var`` 
fields`` 
=`` 
new`` 
List`` !
<``! "
Field``" '
>``' (
(``( )
)``) *
;``* +
varbb 
parambb 
=bb 

Expressionbb "
.bb" #
	Parameterbb# ,
(bb, -
typebb- 1
)bb1 2
;bb2 3
ifcc 
(cc 
typecc 
.cc 
IsArraycc 
||cc 
typecc  $
.cc$ %
IsEnumerableOrArraycc% 8
(cc8 9
)cc9 :
)cc: ;
returndd 
fieldsdd 
;dd 
foreachff 
(ff 
varff 
propff 
inff  
typeff! %
.ff% &
GetPropertiesff& 3
(ff3 4
)ff4 5
)ff5 6
{gg 
ifhh 
(hh 
ignorePropshh 
.hh  
Containshh  (
(hh( )
prophh) -
.hh- .
Namehh. 2
)hh2 3
||hh4 6
prophh7 ;
.hh; <
GetCustomAttributehh< N
(hhN O
typeofhhO U
(hhU V"
GraphQLIgnoreAttributehhV l
)hhl m
)hhm n
!=hho q
nullhhr v
)hhv w
{ii 
continuejj 
;jj 
}kk 
stringnn 
descriptionnn "
=nn# $
$strnn% '
;nn' (
varoo 
doo 
=oo 
(oo  
DescriptionAttributeoo -
)oo- .
propoo. 2
.oo2 3
GetCustomAttributeoo3 E
(ooE F
typeofooF L
(ooL M 
DescriptionAttributeooM a
)ooa b
,oob c
falseood i
)ooi j
;ooj k
ifpp 
(pp 
dpp 
!=pp 
nullpp 
)pp 
{qq 
descriptionrr 
=rr  !
drr" #
.rr# $
Descriptionrr$ /
;rr/ 0
}ss 
LambdaExpressionuu  
leuu! #
=uu$ %

Expressionuu& 0
.uu0 1
Lambdauu1 7
(uu7 8

Expressionuu8 B
.uuB C
PropertyuuC K
(uuK L
paramuuL Q
,uuQ R
propuuS W
.uuW X
NameuuX \
)uu\ ]
,uu] ^
paramuu_ d
)uud e
;uue f
varvv 
fvv 
=vv 
newvv 
Fieldvv !
(vv! "
SchemaGeneratorvv" 1
.vv1 2"
ToCamelCaseStartsLowervv2 H
(vvH I
propvvI M
.vvM N
NamevvN R
)vvR S
,vvS T
levvU W
,vvW X
descriptionvvY d
)vvd e
;vve f
fieldsww 
.ww 
Addww 
(ww 
fww 
)ww 
;ww 
	CacheTypexx 
<xx 
TContextTypexx &
>xx& '
(xx' (
propxx( ,
.xx, -
PropertyTypexx- 9
,xx9 :
schemaxx; A
)xxA B
;xxB C
}yy 
foreachzz 
(zz 
varzz 
propzz 
inzz  
typezz! %
.zz% &
	GetFieldszz& /
(zz/ 0
)zz0 1
)zz1 2
{{{ 
LambdaExpression||  
le||! #
=||$ %

Expression||& 0
.||0 1
Lambda||1 7
(||7 8

Expression||8 B
.||B C
Field||C H
(||H I
param||I N
,||N O
prop||P T
.||T U
Name||U Y
)||Y Z
,||Z [
param||\ a
)||a b
;||b c
var}} 
f}} 
=}} 
new}} 
Field}} !
(}}! "
SchemaGenerator}}" 1
.}}1 2"
ToCamelCaseStartsLower}}2 H
(}}H I
prop}}I M
.}}M N
Name}}N R
)}}R S
,}}S T
le}}U W
,}}W X
prop}}Y ]
.}}] ^
Name}}^ b
)}}b c
;}}c d
fields~~ 
.~~ 
Add~~ 
(~~ 
f~~ 
)~~ 
;~~ 
	CacheType 
< 
TContextType &
>& '
(' (
prop( ,
., -
	FieldType- 6
,6 7
schema8 >
)> ?
;? @
}
�� 
return
�� 
fields
�� 
;
�� 
}
�� 	
private
�� 
static
�� 
void
�� 
	CacheType
�� %
<
��% &
TContextType
��& 2
>
��2 3
(
��3 4
Type
��4 8
propType
��9 A
,
��A B"
MappedSchemaProvider
��D X
<
��X Y
TContextType
��Y e
>
��e f
schema
��g m
)
��m n
{
�� 	
if
�� 
(
�� 
propType
�� 
.
�� !
IsEnumerableOrArray
�� ,
(
��, -
)
��- .
)
��. /
{
�� 
propType
�� 
=
�� 
propType
�� #
.
��# $&
GetEnumerableOrArrayType
��$ <
(
��< =
)
��= >
;
��> ?
}
�� 
if
�� 
(
�� 
!
�� 
schema
�� 
.
�� 
HasType
�� 
(
��  
propType
��  (
.
��( )
Name
��) -
)
��- .
&&
��/ 1
!
��2 3
ignoreTypes
��3 >
.
��> ?
Contains
��? G
(
��G H
propType
��H P
.
��P Q
Name
��Q U
)
��U V
&&
��W Y
(
��Z [
propType
��[ c
.
��c d
GetTypeInfo
��d o
(
��o p
)
��p q
.
��q r
IsClass
��r y
||
��z |
propType��} �
.��� �
GetTypeInfo��� �
(��� �
)��� �
.��� �
IsInterface��� �
)��� �
)��� �
{
�� 
var
�� 

parameters
�� 
=
��  
new
��! $
List
��% )
<
��) *

Expression
��* 4
>
��4 5
{
��6 7

Expression
��7 A
.
��A B
Constant
��B J
(
��J K
propType
��K S
.
��S T
Name
��T X
)
��X Y
,
��Y Z

Expression
��[ e
.
��e f
Constant
��f n
(
��n o
$str
��o q
)
��q r
,
��r s

Expression
��t ~
.
��~ 
Constant�� �
(��� �
null��� �
)��� �
}��� �
;��� �
var
�� 
method
�� 
=
�� 
schema
�� #
.
��# $
GetType
��$ +
(
��+ ,
)
��, -
.
��- .
	GetMethod
��. 7
(
��7 8
$str
��8 A
,
��A B
new
��C F
[
��G H
]
��H I
{
��J K
typeof
��K Q
(
��Q R
string
��R X
)
��X Y
,
��Y Z
typeof
��[ a
(
��a b
string
��b h
)
��h i
}
��i j
)
��j k
;
��k l
method
�� 
=
�� 
method
�� 
.
��  
MakeGenericMethod
��  1
(
��1 2
propType
��2 :
)
��: ;
;
��; <
var
�� 
t
�� 
=
�� 
(
�� 
ISchemaType
�� $
)
��$ %
method
��% +
.
��+ ,
Invoke
��, 2
(
��2 3
schema
��3 9
,
��9 :
new
��; >
object
��? E
[
��E F
]
��F G
{
��H I
propType
��J R
.
��R S
Name
��S W
,
��W X
propType
��Y a
.
��a b
Name
��b f
+
��g h
$str
��i w
}
��x y
)
��y z
;
��z {
var
�� 
fields
�� 
=
�� )
AddFieldsFromObjectToSchema
�� 8
<
��8 9
TContextType
��9 E
>
��E F
(
��F G
propType
��G O
,
��O P
schema
��Q W
)
��W X
;
��X Y
t
�� 
.
�� 
	AddFields
�� 
(
�� 
fields
�� "
)
��" #
;
��# $
}
�� 
}
�� 	
}
�� 
}�� ��
DY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\SchemaGenerator.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{		 
public

 

class

 
SchemaGenerator

  
{ 
public 
static 
readonly 

Dictionary )
<) *
Type* .
,. /
string0 6
>6 7
DefaultTypeMappings8 K
=L M
newN Q

DictionaryR \
<\ ]
Type] a
,a b
stringc i
>i j
{k l
{ 
typeof 
( 
string 
) 
, 
$str %
}% &
,& '
{ 
typeof 
( 
RequiredField !
<! "
string" (
>( )
)) *
,* +
$str, 5
}5 6
,6 7
{ 
typeof 
( 
Guid 
) 
, 
$str 
}  
,  !
{ 
typeof 
( 
Guid 
? 
) 
, 
$str  
}  !
,! "
{ 
typeof 
( 
RequiredField !
<! "
Guid" &
>& '
)' (
,( )
$str* /
}/ 0
,0 1
{ 
typeof 
( 
int 
) 
, 
$str 
}  
,  !
{ 
typeof 
( 
int 
? 
) 
, 
$str  
}  !
,! "
{ 
typeof 
( 
RequiredField !
<! "
int" %
>% &
)& '
,' (
$str) /
}/ 0
,0 1
{ 
typeof 
( 
Int16 
) 
, 
$str !
}! "
," #
{ 
typeof 
( 
Int16 
? 
) 
, 
$str "
}" #
,# $
{ 
typeof 
( 
RequiredField !
<! "
Int16" '
>' (
)( )
,) *
$str+ 1
}1 2
,2 3
{ 
typeof 
( 
double 
) 
, 
$str $
}$ %
,% &
{ 
typeof 
( 
double 
? 
) 
, 
$str %
}% &
,& '
{ 
typeof 
( 
RequiredField !
<! "
double" (
>( )
)) *
,* +
$str, 4
}4 5
,5 6
{   
typeof   
(   
float   
)   
,   
$str   #
}  # $
,  $ %
{!! 
typeof!! 
(!! 
float!! 
?!! 
)!! 
,!! 
$str!! $
}!!$ %
,!!% &
{"" 
typeof"" 
("" 
RequiredField"" !
<""! "
float""" '
>""' (
)""( )
,"") *
$str""+ 3
}""3 4
,""4 5
{$$ 
typeof$$ 
($$ 
bool$$ 
)$$ 
,$$ 
$str$$ $
}$$$ %
,$$% &
{%% 
typeof%% 
(%% 
bool%% 
?%% 
)%% 
,%% 
$str%% %
}%%% &
,%%& '
{&& 
typeof&& 
(&& 
RequiredField&& !
<&&! "
bool&&" &
>&&& '
)&&' (
,&&( )
$str&&* 4
}&&4 5
,&&5 6
{(( 
typeof(( 
((( 
EntityQueryType(( #
<((# $
>(($ %
)((% &
,((& '
$str((( 0
}((0 1
,((1 2
{** 
typeof** 
(** 
long** 
)** 
,** 
$str**  
}**  !
,**! "
{++ 
typeof++ 
(++ 
long++ 
?++ 
)++ 
,++ 
$str++ !
}++! "
,++" #
{,, 
typeof,, 
(,, 
RequiredField,, !
<,,! "
long,," &
>,,& '
),,' (
,,,( )
$str,,* 0
},,0 1
,,,1 2
{.. 
typeof.. 
(.. 
DateTime.. 
).. 
,.. 
$str.. '
}..' (
,..( )
{// 
typeof// 
(// 
DateTime// 
?// 
)// 
,// 
$str//  (
}//( )
,//) *
{00 
typeof00 
(00 
RequiredField00 !
<00! "
DateTime00" *
>00* +
)00+ ,
,00, -
$str00. 7
}007 8
,008 9
{22 
typeof22 
(22 
uint22 
)22 
,22 
$str22  
}22  !
,22! "
{33 
typeof33 
(33 
uint33 
?33 
)33 
,33 
$str33 !
}33! "
,33" #
{44 
typeof44 
(44 
RequiredField44 !
<44! "
uint44" &
>44& '
)44' (
,44( )
$str44* 0
}440 1
,441 2
{66 
typeof66 
(66 
UInt1666 
)66 
,66 
$str66 "
}66" #
,66# $
{77 
typeof77 
(77 
UInt1677 
?77 
)77 
,77 
$str77 #
}77# $
,77$ %
{88 
typeof88 
(88 
RequiredField88 !
<88! "
UInt1688" (
>88( )
)88) *
,88* +
$str88, 2
}882 3
,883 4
}99 	
;99	 

internal;; 
static;; 
string;; 
Make;; #
(;;# $
ISchemaProvider;;$ 3
schema;;4 :
,;;: ;
IReadOnlyDictionary;;< O
<;;O P
Type;;P T
,;;T U
string;;V \
>;;\ ]
typeMappings;;^ j
,;;j k

Dictionary;;l v
<;;v w
Type;;w {
,;;{ |
string	;;} �
>
;;� �!
customScalarMapping
;;� �
)
;;� �
{<< 	
var>> 
combinedMapping>> 
=>>  !
DefaultTypeMappings>>" 5
.>>5 6
ToDictionary>>6 B
(>>B C
k>>C D
=>>>E G
k>>H I
.>>I J
Key>>J M
,>>M N
v>>O P
=>>>Q S
v>>T U
.>>U V
Value>>V [
)>>[ \
;>>\ ]
foreach?? 
(?? 
var?? 
item?? 
in??  
typeMappings??! -
)??- .
{@@ 
combinedMappingAA 
[AA  
itemAA  $
.AA$ %
KeyAA% (
]AA( )
=AA* +
itemAA, 0
.AA0 1
ValueAA1 6
;AA6 7
}BB 
varDD 
scalarsDD 
=DD 
newDD 
StringBuilderDD +
(DD+ ,
)DD, -
;DD- .
foreachEE 
(EE 
varEE 
itemEE 
inEE  
customScalarMappingEE! 4
)EE4 5
{FF 
scalarsGG 
.GG 

AppendLineGG "
(GG" #
$"GG# %
scalar GG% ,
{GG, -
itemGG- 1
.GG1 2
ValueGG2 7
}GG7 8
"GG8 9
)GG9 :
;GG: ;
combinedMappingHH 
[HH  
itemHH  $
.HH$ %
KeyHH% (
]HH( )
=HH* +
itemHH, 0
.HH0 1
ValueHH1 6
;HH6 7
}II 
varKK 
typesKK 
=KK 
BuildSchemaTypesKK (
(KK( )
schemaKK) /
,KK/ 0
combinedMappingKK1 @
)KK@ A
;KKA B
varLL 
	mutationsLL 
=LL 
BuildMutationsLL *
(LL* +
schemaLL+ 1
,LL1 2
combinedMappingLL3 B
)LLB C
;LLC D
varNN 

queryTypesNN 
=NN 
MakeQueryTypeNN *
(NN* +
schemaNN+ 1
,NN1 2
combinedMappingNN3 B
)NNB C
;NNC D
returnPP 
$@"PP I
?schema {{
    query: RootQuery
    mutation: Mutation
}}

PU
{UU 
scalarsUU 
}UU 	!


type RootQuery {{
UX	
{XX 

queryTypesXX 
}XX 

}}
XZ
{ZZ 
typesZZ 
}ZZ  


type Mutation {{
Z]
{]] 
	mutations]] 

}]]
 

}}]^ 
"^^ 
;^^ 
}__ 	
privateaa 
staticaa 
stringaa 
BuildMutationsaa ,
(aa, -
ISchemaProvideraa- <
schemaaa= C
,aaC D
IReadOnlyDictionaryaaE X
<aaX Y
TypeaaY ]
,aa] ^
stringaa_ e
>aae f
combinedMappingaag v
)aav w
{bb 	
varcc 
	mutationscc 
=cc 
newcc 
StringBuildercc  -
(cc- .
)cc. /
;cc/ 0
foreachdd 
(dd 
vardd 
itemdd 
indd  
schemadd! '
.dd' (
GetMutationsdd( 4
(dd4 5
)dd5 6
)dd6 7
{ee 
ifff 
(ff 
!ff 
stringff 
.ff 
IsNullOrEmptyff )
(ff) *
itemff* .
.ff. /
Descriptionff/ :
)ff: ;
)ff; <
	mutationsgg 
.gg 

AppendLinegg (
(gg( )
$"gg) +
\t\"gg+ /
{gg/ 0
itemgg0 4
.gg4 5
Descriptiongg5 @
}gg@ A
\"ggA C
"ggC D
)ggD E
;ggE F
	mutationsii 
.ii 

AppendLineii $
(ii$ %
$"ii% '
\tii' )
{ii) *"
ToCamelCaseStartsLowerii* @
(ii@ A
itemiiA E
.iiE F
NameiiF J
)iiJ K
}iiK L
{iiL M

GetGqlArgsiiM W
(iiW X
itemiiX \
,ii\ ]
schemaii^ d
,iid e
combinedMappingiif u
,iiu v
$striiw {
)ii{ |
}ii| }
: ii} 
{	ii �
GetGqlReturnType
ii� �
(
ii� �
item
ii� �
,
ii� �
schema
ii� �
,
ii� �
combinedMapping
ii� �
)
ii� �
}
ii� �
"
ii� �
)
ii� �
;
ii� �
}jj 
returnll 
	mutationsll 
.ll 
ToStringll %
(ll% &
)ll& '
;ll' (
}mm 	
privateoo 
staticoo 
stringoo 
BuildSchemaTypesoo .
(oo. /
ISchemaProvideroo/ >
schemaoo? E
,ooE F
IReadOnlyDictionaryooG Z
<ooZ [
Typeoo[ _
,oo_ `
stringooa g
>oog h
combinedMappingooi x
)oox y
{pp 	
varqq 
typesqq 
=qq 
newqq 
StringBuilderqq )
(qq) *
)qq* +
;qq+ ,
foreachrr 
(rr 
varrr 
typeItemrr !
inrr" $
schemarr% +
.rr+ ,
GetNonContextTypesrr, >
(rr> ?
)rr? @
)rr@ A
{ss 
typestt 
.tt 

AppendLinett  
(tt  !
)tt! "
;tt" #
typesxx 
.xx 

AppendLinexx  
(xx  !
$"xx! #
{xx# $
(xx$ %
typeItemxx% -
.xx- .
IsInputxx. 5
?xx6 7
$strxx8 ?
:xx@ A
$strxxB H
)xxH I
}xxI J
{xxK L
typeItemxxL T
.xxT U
NamexxU Y
}xxY Z
 {{xxZ ]
"xx] ^
)xx^ _
;xx_ `
foreachyy 
(yy 
varyy 
fieldyy "
inyy# %
typeItemyy& .
.yy. /
	GetFieldsyy/ 8
(yy8 9
)yy9 :
)yy: ;
{zz 
if{{ 
({{ 
field{{ 
.{{ 
Name{{ "
.{{" #

StartsWith{{# -
({{- .
$str{{. 2
){{2 3
){{3 4
continue||  
;||  !
if~~ 
(~~ 
!~~ 
string~~ 
.~~  
IsNullOrEmpty~~  -
(~~- .
field~~. 3
.~~3 4
Description~~4 ?
)~~? @
)~~@ A
types 
. 

AppendLine (
(( )
$") +
\t\"+ /
{/ 0
field0 5
.5 6
Description6 A
}A B
\"B D
"D E
)E F
;F G
types
�� 
.
�� 

AppendLine
�� $
(
��$ %
$"
��% '
\t
��' )
{
��) *$
ToCamelCaseStartsLower
��* @
(
��@ A
field
��A F
.
��F G
Name
��G K
)
��K L
}
��L M
{
��M N

GetGqlArgs
��N X
(
��X Y
field
��Y ^
,
��^ _
schema
��` f
,
��f g
combinedMapping
��h w
)
��w x
}
��x y
: 
��y {
{
��{ |
GetGqlReturnType��| �
(��� �
field��� �
,��� �
schema��� �
,��� �
combinedMapping��� �
)��� �
}��� �
"��� �
)��� �
;��� �
}
�� 
types
�� 
.
�� 

AppendLine
��  
(
��  !
$str
��! $
)
��$ %
;
��% &
}
�� 
return
�� 
types
�� 
.
�� 
ToString
�� !
(
��! "
)
��" #
;
��# $
}
�� 	
private
�� 
static
�� 
object
�� 
GetGqlReturnType
�� .
(
��. /
IMethodType
��/ :
field
��; @
,
��@ A
ISchemaProvider
��B Q
schema
��R X
,
��X Y!
IReadOnlyDictionary
��Z m
<
��m n
Type
��n r
,
��r s
string
��t z
>
��z {
combinedMapping��| �
)��� �
{
�� 	
return
�� 
field
�� 
.
�� 
IsEnumerable
�� %
?
��& '
$str
��( +
+
��, -
ClrToGqlType
��. :
(
��: ;
field
��; @
.
��@ A
ReturnTypeClr
��A N
.
��N O&
GetEnumerableOrArrayType
��O g
(
��g h
)
��h i
,
��i j
schema
��k q
,
��q r
combinedMapping��s �
)��� �
+��� �
$str��� �
:��� �
ClrToGqlType��� �
(��� �
field��� �
.��� �
ReturnTypeClr��� �
,��� �
schema��� �
,��� �
combinedMapping��� �
)��� �
;��� �
}
�� 	
private
�� 
static
�� 
object
�� 

GetGqlArgs
�� (
(
��( )
IMethodType
��) 4
field
��5 :
,
��: ;
ISchemaProvider
��< K
schema
��L R
,
��R S!
IReadOnlyDictionary
��T g
<
��g h
Type
��h l
,
��l m
string
��n t
>
��t u
combinedMapping��v �
,��� �
string��� �
noArgs��� �
=��� �
$str��� �
)��� �
{
�� 	
if
�� 
(
�� 
field
�� 
.
�� 
	Arguments
�� 
==
��  "
null
��# '
||
��( *
!
��+ ,
field
��, 1
.
��1 2
	Arguments
��2 ;
.
��; <
Any
��< ?
(
��? @
)
��@ A
)
��A B
return
�� 
noArgs
�� 
;
�� 
var
�� 
all
�� 
=
�� 
field
�� 
.
�� 
	Arguments
�� %
.
��% &
Select
��& ,
(
��, -
f
��- .
=>
��/ 1$
ToCamelCaseStartsLower
��2 H
(
��H I
f
��I J
.
��J K
Key
��K N
)
��N O
+
��P Q
$str
��R V
+
��W X
ClrToGqlType
��Y e
(
��e f
f
��f g
.
��g h
Value
��h m
,
��m n
schema
��o u
,
��u v
combinedMapping��w �
)��� �
)��� �
;��� �
return
�� 
$"
�� 
(
�� 
{
�� 
string
�� 
.
�� 
Join
�� "
(
��" #
$str
��# '
,
��' (
all
��) ,
)
��, -
}
��- .
)
��. /
"
��/ 0
;
��0 1
}
�� 	
private
�� 
static
�� 
string
�� 
ClrToGqlType
�� *
(
��* +
Type
��+ /
type
��0 4
,
��4 5
ISchemaProvider
��6 E
schema
��F L
,
��L M!
IReadOnlyDictionary
��N a
<
��a b
Type
��b f
,
��f g
string
��h n
>
��n o
combinedMapping
��p 
)�� �
{
�� 	
if
�� 
(
�� 
!
�� 
combinedMapping
��  
.
��  !
ContainsKey
��! ,
(
��, -
type
��- 1
)
��1 2
)
��2 3
{
�� 
if
�� 
(
�� 
schema
�� 
.
�� 
HasType
�� "
(
��" #
type
��# '
)
��' (
)
��( )
{
�� 
return
�� 
schema
�� !
.
��! "*
GetSchemaTypeNameForRealType
��" >
(
��> ?
type
��? C
)
��C D
;
��D E
}
�� 
if
�� 
(
�� 
type
�� 
.
�� !
IsEnumerableOrArray
�� ,
(
��, -
)
��- .
)
��. /
{
�� 
return
�� 
$str
�� 
+
��  
ClrToGqlType
��! -
(
��- .
type
��. 2
.
��2 3!
GetGenericArguments
��3 F
(
��F G
)
��G H
[
��H I
$num
��I J
]
��J K
,
��K L
schema
��M S
,
��S T
combinedMapping
��U d
)
��d e
+
��f g
$str
��h k
;
��k l
}
�� 
if
�� 
(
�� 
type
�� 
.
�� &
IsConstructedGenericType
�� 1
)
��1 2
{
�� 
return
�� 
ClrToGqlType
�� '
(
��' (
type
��( ,
.
��, -&
GetGenericTypeDefinition
��- E
(
��E F
)
��F G
,
��G H
schema
��I O
,
��O P
combinedMapping
��Q `
)
��` a
;
��a b
}
�� 
if
�� 
(
�� 
type
�� 
.
�� 
GetTypeInfo
�� $
(
��$ %
)
��% &
.
��& '
IsEnum
��' -
)
��- .
{
�� 
return
�� 
$str
��  
;
��  !
}
�� 
return
�� 
$str
�� 
;
��  
}
�� 
return
�� 
combinedMapping
�� "
[
��" #
type
��# '
]
��' (
;
��( )
}
�� 	
private
�� 
static
�� 
string
�� 
MakeQueryType
�� +
(
��+ ,
ISchemaProvider
��, ;
schema
��< B
,
��B C!
IReadOnlyDictionary
��D W
<
��W X
Type
��X \
,
��\ ]
string
��^ d
>
��d e
combinedMapping
��f u
)
��u v
{
�� 	
var
�� 
sb
�� 
=
�� 
new
�� 
StringBuilder
�� &
(
��& '
)
��' (
;
��( )
foreach
�� 
(
�� 
var
�� 
t
�� 
in
�� 
schema
�� $
.
��$ %
GetQueryFields
��% 3
(
��3 4
)
��4 5
.
��5 6
OrderBy
��6 =
(
��= >
s
��> ?
=>
��@ B
s
��C D
.
��D E
Name
��E I
)
��I J
)
��J K
{
�� 
if
�� 
(
�� 
t
�� 
.
�� 
Name
�� 
.
�� 

StartsWith
�� %
(
��% &
$str
��& *
)
��* +
)
��+ ,
continue
�� 
;
�� 
var
�� 
typeName
�� 
=
�� 
GetGqlReturnType
�� /
(
��/ 0
t
��0 1
,
��1 2
schema
��3 9
,
��9 :
combinedMapping
��; J
)
��J K
;
��K L
if
�� 
(
�� 
!
�� 
string
�� 
.
�� 
IsNullOrEmpty
�� )
(
��) *
t
��* +
.
��+ ,
Description
��, 7
)
��7 8
)
��8 9
sb
�� 
.
�� 

AppendLine
�� !
(
��! "
$"
��" $
\t\"
��$ (
{
��( )
t
��) *
.
��* +
Description
��+ 6
}
��6 7
\"
��7 9
"
��9 :
)
��: ;
;
��; <
sb
�� 
.
�� 

AppendLine
�� 
(
�� 
$"
��  
\t
��  "
{
��" #$
ToCamelCaseStartsLower
��# 9
(
��9 :
t
��: ;
.
��; <
Name
��< @
)
��@ A
}
��A B
{
��B C

GetGqlArgs
��C M
(
��M N
t
��N O
,
��O P
schema
��Q W
,
��W X
combinedMapping
��Y h
)
��h i
}
��i j
: 
��j l
{
��l m
typeName
��m u
}
��u v
"
��v w
)
��w x
;
��x y
}
�� 
return
�� 
sb
�� 
.
�� 
ToString
�� 
(
�� 
)
��  
;
��  !
}
�� 	
public
�� 
static
�� 
string
�� $
ToCamelCaseStartsLower
�� 3
(
��3 4
string
��4 :
name
��; ?
)
��? @
{
�� 	
return
�� 
name
�� 
.
�� 
	Substring
�� !
(
��! "
$num
��" #
,
��# $
$num
��% &
)
��& '
.
��' (
ToLowerInvariant
��( 8
(
��8 9
)
��9 :
+
��; <
name
��= A
.
��A B
	Substring
��B K
(
��K L
$num
��L M
)
��M N
;
��N O
}
�� 	
}
�� 
}�� ��
HY:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\SchemaIntrospection.cs
	namespace 	
EntityGraphQL
 
. 
Schema 
{ 
public

 

class

 
SchemaIntrospection

 $
{ 
public 
static 
Models 
. 
Schema #
Make$ (
(( )
ISchemaProvider) 8
schema9 ?
,? @
IReadOnlyDictionaryA T
<T U
TypeU Y
,Y Z
string[ a
>a b
combinedMappingc r
)r s
{ 	
var 
types 
= 
new 
List  
<  !
Models! '
.' (
TypeElement( 3
>3 4
{ 
new 
TypeElement 
{ 
Description 
=  !
$str" l
,l m
Kind 
= 
$str #
,# $
Name 
= 
$str "
," #
OfType 
= 
null !
,! "
} 
, 
new 
TypeElement 
{ 
Description 
=  !
$str" e
,e f
Kind   
=   
$str   #
,  # $
Name!! 
=!! 
$str!! %
,!!% &
OfType"" 
="" 
null"" !
,""! "
}## 
,## 
}$$ 
;$$ 
types%% 
.%% 
AddRange%% 
(%% 
BuildQueryTypes%% *
(%%* +
schema%%+ 1
,%%1 2
combinedMapping%%3 B
)%%B C
)%%C D
;%%D E
types&& 
.&& 
AddRange&& 
(&& 
BuildInputTypes&& *
(&&* +
schema&&+ 1
,&&1 2
combinedMapping&&3 B
)&&B C
)&&C D
;&&D E
types'' 
.'' 
AddRange'' 
('' 
BuildEnumTypes'' )
('') *
schema''* 0
,''0 1
combinedMapping''2 A
)''A B
)''B C
;''C D
types(( 
.(( 
AddRange(( 
((( 
BuildScalarTypes(( +
(((+ ,
schema((, 2
,((2 3
combinedMapping((4 C
)((C D
)((D E
;((E F
var** 
schemaDescription** !
=**" #
new**$ '
Models**( .
.**. /
Schema**/ 5
{++ 
	QueryType,, 
=,, 
new,, 
Models,,  &
.,,& '
TypeElement,,' 2
{-- 
Name.. 
=.. 
$str.. "
}// 
,// 
MutationType00 
=00 
new00 "
Models00# )
.00) *
TypeElement00* 5
{11 
Name22 
=22 
$str22 %
}33 
,33 
Types44 
=44 
types44 
.44 
OrderBy44 %
(44% &
x44& '
=>44( *
x44+ ,
.44, -
Name44- 1
)441 2
.442 3
ToArray443 :
(44: ;
)44; <
,44< =

Directives55 
=55 
BuildDirectives55 ,
(55, -
)55- .
.55. /
ToArray55/ 6
(556 7
)557 8
}66 
;66 
return88 
schemaDescription88 $
;88$ %
}99 	
private;; 
static;; 
IEnumerable;; "
<;;" #
TypeElement;;# .
>;;. /
BuildScalarTypes;;0 @
(;;@ A
ISchemaProvider;;A P
schema;;Q W
,;;W X
IReadOnlyDictionary;;Y l
<;;l m
Type;;m q
,;;q r
string;;s y
>;;y z
combinedMapping	;;{ �
)
;;� �
{<< 	
var== 
types== 
=== 
new== 
List==  
<==  !
Models==! '
.==' (
TypeElement==( 3
>==3 4
(==4 5
)==5 6
;==6 7
foreach?? 
(?? 
var?? 
customScalar?? %
in??& (
schema??) /
.??/ 0
CustomScalarTypes??0 A
)??A B
{@@ 
varAA 
typeElementAA 
=AA  !
newAA" %
ModelsAA& ,
.AA, -
TypeElementAA- 8
{BB 
KindCC 
=CC 
$strCC #
,CC# $
NameDD 
=DD 
customScalarDD '
,DD' (
DescriptionEE 
=EE  !
nullEE" &
,EE& '
}FF 
;FF 
typesHH 
.HH 
AddHH 
(HH 
typeElementHH %
)HH% &
;HH& '
}II 
returnKK 
typesKK 
;KK 
}LL 	
privateNN 
staticNN 
ListNN 
<NN 
ModelsNN "
.NN" #
TypeElementNN# .
>NN. /
BuildQueryTypesNN0 ?
(NN? @
ISchemaProviderNN@ O
schemaNNP V
,NNV W
IReadOnlyDictionaryNNX k
<NNk l
TypeNNl p
,NNp q
stringNNr x
>NNx y
combinedMapping	NNz �
)
NN� �
{OO 	
varPP 
typesPP 
=PP 
newPP 
ListPP  
<PP  !
ModelsPP! '
.PP' (
TypeElementPP( 3
>PP3 4
(PP4 5
)PP5 6
;PP6 7
foreachRR 
(RR 
varRR 
stRR 
inRR 
schemaRR %
.RR% &
GetNonContextTypesRR& 8
(RR8 9
)RR9 :
.RR: ;
WhereRR; @
(RR@ A
sRRA B
=>RRC E
!RRF G
sRRG H
.RRH I
IsInputRRI P
)RRP Q
)RRQ R
{SS 
varTT 
typeElementTT 
=TT  !
newTT" %
ModelsTT& ,
.TT, -
TypeElementTT- 8
{UU 
KindVV 
=VV 
$strVV #
,VV# $
NameWW 
=WW 
stWW 
.WW 
NameWW "
,WW" #
DescriptionXX 
=XX  !
stXX" $
.XX$ %
DescriptionXX% 0
}YY 
;YY 
types[[ 
.[[ 
Add[[ 
([[ 
typeElement[[ %
)[[% &
;[[& '
}\\ 
return^^ 
types^^ 
;^^ 
}__ 	
privatejj 
staticjj 
Listjj 
<jj 
Modelsjj "
.jj" #
TypeElementjj# .
>jj. /
BuildInputTypesjj0 ?
(jj? @
ISchemaProviderjj@ O
schemajjP V
,jjV W
IReadOnlyDictionaryjjX k
<jjk l
Typejjl p
,jjp q
stringjjr x
>jjx y
combinedMapping	jjz �
)
jj� �
{kk 	
varll 
typesll 
=ll 
newll 
Listll  
<ll  !
Modelsll! '
.ll' (
TypeElementll( 3
>ll3 4
(ll4 5
)ll5 6
;ll6 7
foreachnn 
(nn 
ISchemaTypenn  

schemaTypenn! +
innn, .
schemann/ 5
.nn5 6
GetNonContextTypesnn6 H
(nnH I
)nnI J
.nnJ K
WherennK P
(nnP Q
snnQ R
=>nnS U
snnV W
.nnW X
IsInputnnX _
)nn_ `
)nn` a
{oo 
ifpp 
(pp 

schemaTypepp 
.pp 
Namepp #
.pp# $

StartsWithpp$ .
(pp. /
$strpp/ 3
)pp3 4
)pp4 5
continueqq 
;qq 
varss 
inputValuesss 
=ss  !
newss" %
Listss& *
<ss* +
Modelsss+ 1
.ss1 2

InputValuess2 <
>ss< =
(ss= >
)ss> ?
;ss? @
foreachtt 
(tt 
Fieldtt 
fieldtt $
intt% '

schemaTypett( 2
.tt2 3
	GetFieldstt3 <
(tt< =
)tt= >
)tt> ?
{uu 
ifvv 
(vv 
fieldvv 
.vv 
Namevv "
.vv" #

StartsWithvv# -
(vv- .
$strvv. 2
)vv2 3
)vv3 4
continueww  
;ww  !
varzz 
propertyzz  
=zz! "

schemaTypezz# -
.zz- .
ContextTypezz. 9
.zz9 :
GetPropertyzz: E
(zzE F
fieldzzF K
.zzK L
NamezzL P
)zzP Q
;zzQ R
if{{ 
({{ 
property{{  
!={{! #
null{{$ (
&&{{) +
property{{, 4
.{{4 5
GetCustomAttribute{{5 G
({{G H
typeof{{H N
({{N O"
GraphQLIgnoreAttribute{{O e
){{e f
){{f g
!={{h j
null{{k o
){{o p
continue||  
;||  !
if 
( 
field 
. 
Resolve %
.% &
NodeType& .
==/ 1
System2 8
.8 9
Linq9 =
.= >
Expressions> I
.I J
ExpressionTypeJ X
.X Y
CallY ]
)] ^
continue
��  
;
��  !
if
�� 
(
�� 
field
�� 
.
�� 
ReturnTypeClr
�� +
.
��+ ,
GetTypeInfo
��, 7
(
��7 8
)
��8 9
.
��9 :
IsEnum
��: @
)
��@ A
continue
��  
;
��  !
inputValues
�� 
.
��  
Add
��  #
(
��# $
new
��$ '
Models
��( .
.
��. /

InputValue
��/ 9
{
�� 
Name
�� 
=
�� 
field
�� $
.
��$ %
Name
��% )
,
��) *
Description
�� #
=
��$ %
field
��& +
.
��+ ,
Description
��, 7
,
��7 8
Type
�� 
=
�� 
	BuildType
�� (
(
��( )
schema
��) /
,
��/ 0
field
��1 6
.
��6 7
ReturnTypeClr
��7 D
,
��D E
field
��F K
.
��K L
ReturnTypeSingle
��L \
,
��\ ]
combinedMapping
��^ m
,
��m n
true
��o s
)
��s t
}
�� 
)
�� 
;
�� 
}
�� 
var
�� 
typeElement
�� 
=
��  !
new
��" %
Models
��& ,
.
��, -
TypeElement
��- 8
{
�� 
Kind
�� 
=
�� 
$str
�� )
,
��) *
Name
�� 
=
�� 

schemaType
�� %
.
��% &
Name
��& *
,
��* +
Description
�� 
=
��  !

schemaType
��" ,
.
��, -
Description
��- 8
,
��8 9
InputFields
�� 
=
��  !
inputValues
��" -
.
��- .
ToArray
��. 5
(
��5 6
)
��6 7
}
�� 
;
�� 
types
�� 
.
�� 
Add
�� 
(
�� 
typeElement
�� %
)
��% &
;
��& '
}
�� 
return
�� 
types
�� 
;
�� 
}
�� 	
private
�� 
static
�� 
List
�� 
<
�� 
Models
�� "
.
��" #
TypeElement
��# .
>
��. /
BuildEnumTypes
��0 >
(
��> ?
ISchemaProvider
��? N
schema
��O U
,
��U V!
IReadOnlyDictionary
��W j
<
��j k
Type
��k o
,
��o p
string
��q w
>
��w x
combinedMapping��y �
)��� �
{
�� 	
var
�� 
types
�� 
=
�� 
new
�� 
List
��  
<
��  !
Models
��! '
.
��' (
TypeElement
��( 3
>
��3 4
(
��4 5
)
��5 6
;
��6 7
foreach
�� 
(
�� 
ISchemaType
��  

schemaType
��! +
in
��, .
schema
��/ 5
.
��5 6 
GetNonContextTypes
��6 H
(
��H I
)
��I J
)
��J K
{
�� 
var
�� 
typeElement
�� 
=
��  !
new
��" %
Models
��& ,
.
��, -
TypeElement
��- 8
{
�� 
Kind
�� 
=
�� 
$str
�� !
,
��! "
Name
�� 
=
�� 
string
�� !
.
��! "
Empty
��" '
,
��' (
Description
�� 
=
��  !
null
��" &
,
��& '

EnumValues
�� 
=
��  
new
��! $
Models
��% +
.
��+ ,
	EnumValue
��, 5
[
��5 6
]
��6 7
{
��8 9
}
��: ;
}
�� 
;
�� 
var
�� 
	enumTypes
�� 
=
�� 
new
��  #
List
��$ (
<
��( )
Models
��) /
.
��/ 0
	EnumValue
��0 9
>
��9 :
(
��: ;
)
��; <
;
��< =
foreach
�� 
(
�� 
Field
�� 
field
�� $
in
��% '

schemaType
��( 2
.
��2 3
	GetFields
��3 <
(
��< =
)
��= >
.
��> ?
Where
��? D
(
��D E
x
��E F
=>
��G I
x
��J K
.
��K L
ReturnTypeClr
��L Y
.
��Y Z
GetTypeInfo
��Z e
(
��e f
)
��f g
.
��g h
IsEnum
��h n
)
��n o
)
��o p
{
�� 
if
�� 
(
�� 
field
�� 
.
�� 
Name
�� "
.
��" #

StartsWith
��# -
(
��- .
$str
��. 2
)
��2 3
)
��3 4
continue
��  
;
��  !
typeElement
�� 
.
��  
Name
��  $
=
��% &
field
��' ,
.
��, -
ReturnTypeSingle
��- =
;
��= >
typeElement
�� 
.
��  
Description
��  +
=
��, -
field
��. 3
.
��3 4
Description
��4 ?
;
��? @
foreach
�� 
(
�� 
var
��  
	fieldInfo
��! *
in
��+ -
field
��. 3
.
��3 4
ReturnTypeClr
��4 A
.
��A B
	GetFields
��B K
(
��K L
)
��L M
)
��M N
{
�� 
if
�� 
(
�� 
	fieldInfo
�� %
.
��% &
Name
��& *
==
��+ -
$str
��. 7
)
��7 8
continue
�� $
;
��$ %
var
�� 
	attribute
�� %
=
��& '
(
��( )
System
��) /
.
��/ 0
ComponentModel
��0 >
.
��> ?"
DescriptionAttribute
��? S
)
��S T
	fieldInfo
��T ]
.
��] ^ 
GetCustomAttribute
��^ p
(
��p q
typeof
��q w
(
��w x
System
��x ~
.
��~ 
ComponentModel�� �
.��� �$
DescriptionAttribute��� �
)��� �
)��� �
;��� �
	enumTypes
�� !
.
��! "
Add
��" %
(
��% &
new
��& )
Models
��* 0
.
��0 1
	EnumValue
��1 :
{
�� 
Name
��  
=
��! "
	fieldInfo
��# ,
.
��, -
Name
��- 1
,
��1 2
Description
�� '
=
��( )
	attribute
��* 3
?
��3 4
.
��4 5
Description
��5 @
,
��@ A
IsDeprecated
�� (
=
��) *
false
��+ 0
,
��0 1
DeprecationReason
�� -
=
��. /
null
��0 4
}
�� 
)
�� 
;
�� 
}
�� 
}
�� 
typeElement
�� 
.
�� 

EnumValues
�� &
=
��' (
	enumTypes
��) 2
.
��2 3
ToArray
��3 :
(
��: ;
)
��; <
;
��< =
if
�� 
(
�� 
typeElement
�� 
.
��  

EnumValues
��  *
.
��* +
Count
��+ 0
(
��0 1
)
��1 2
>
��3 4
$num
��5 6
)
��6 7
types
�� 
.
�� 
Add
�� 
(
�� 
typeElement
�� )
)
��) *
;
��* +
}
�� 
return
�� 
types
�� 
;
�� 
}
�� 	
private
�� 
static
�� 
Models
�� 
.
�� 
TypeElement
�� )
	BuildType
��* 3
(
��3 4
ISchemaProvider
��4 C
schema
��D J
,
��J K
Type
��L P
clrType
��Q X
,
��X Y
string
��Z `
gqlTypeName
��a l
,
��l m"
IReadOnlyDictionary��n �
<��� �
Type��� �
,��� �
string��� �
>��� �
combinedMapping��� �
,��� �
bool��� �
isInput��� �
=��� �
false��� �
)��� �
{
�� 	
var
�� 
type
�� 
=
�� 
new
�� 
Models
�� !
.
��! "
TypeElement
��" -
(
��- .
)
��. /
;
��/ 0
if
�� 
(
�� 
clrType
�� 
.
�� !
IsEnumerableOrArray
�� +
(
��+ ,
)
��, -
)
��- .
{
�� 
type
�� 
.
�� 
Kind
�� 
=
�� 
$str
�� "
;
��" #
type
�� 
.
�� 
Name
�� 
=
�� 
null
��  
;
��  !
type
�� 
.
�� 
OfType
�� 
=
�� 
	BuildType
�� '
(
��' (
schema
��( .
,
��. /
clrType
��0 7
.
��7 8&
GetEnumerableOrArrayType
��8 P
(
��P Q
)
��Q R
,
��R S
gqlTypeName
��T _
,
��_ `
combinedMapping
��a p
,
��p q
isInput
��r y
)
��y z
;
��z {
}
�� 
else
�� 
if
�� 
(
�� 
clrType
�� 
.
�� 
Name
�� !
==
��" $
$str
��% 6
)
��6 7
{
�� 
type
�� 
.
�� 
Kind
�� 
=
�� 
$str
�� &
;
��& '
type
�� 
.
�� 
Name
�� 
=
�� 
null
��  
;
��  !
type
�� 
.
�� 
OfType
�� 
=
�� 
	BuildType
�� '
(
��' (
schema
��( .
,
��. /
clrType
��0 7
.
��7 8!
GetGenericArguments
��8 K
(
��K L
)
��L M
[
��M N
$num
��N O
]
��O P
,
��P Q
gqlTypeName
��R ]
,
��] ^
combinedMapping
��_ n
,
��n o
isInput
��p w
)
��w x
;
��x y
}
�� 
else
�� 
if
�� 
(
�� 
clrType
�� 
.
�� 
GetTypeInfo
�� (
(
��( )
)
��) *
.
��* +
IsEnum
��+ 1
)
��1 2
{
�� 
type
�� 
.
�� 
Kind
�� 
=
�� 
$str
�� "
;
��" #
type
�� 
.
�� 
Name
�� 
=
�� 
FindNamedMapping
�� ,
(
��, -
clrType
��- 4
,
��4 5
combinedMapping
��6 E
,
��E F
gqlTypeName
��G R
)
��R S
;
��S T
type
�� 
.
�� 
OfType
�� 
=
�� 
null
�� "
;
��" #
}
�� 
else
�� 
{
�� 
type
�� 
.
�� 
Kind
�� 
=
�� 
combinedMapping
�� +
.
��+ ,
Any
��, /
(
��/ 0
x
��0 1
=>
��2 4
x
��5 6
.
��6 7
Key
��7 :
==
��; =
clrType
��> E
)
��E F
?
��G H
$str
��I Q
:
��R S
$str
��T \
;
��\ ]
type
�� 
.
�� 
OfType
�� 
=
�� 
null
�� "
;
��" #
if
�� 
(
�� 
type
�� 
.
�� 
Kind
�� 
==
��  
$str
��! )
&&
��* ,
isInput
��- 4
)
��4 5
{
�� 
type
�� 
.
�� 
Name
�� 
=
�� 
SchemaGenerator
��  /
.
��/ 0$
ToCamelCaseStartsLower
��0 F
(
��F G
FindNamedMapping
��G W
(
��W X
clrType
��X _
,
��_ `
combinedMapping
��a p
,
��p q
gqlTypeName
��r }
)
��} ~
)
��~ 
;�� �
}
�� 
else
�� 
type
�� 
.
�� 
Name
�� 
=
�� 
FindNamedMapping
��  0
(
��0 1
clrType
��1 8
,
��8 9
combinedMapping
��: I
,
��I J
gqlTypeName
��K V
)
��V W
;
��W X
}
�� 
return
�� 
type
�� 
;
�� 
}
�� 	
public
�� 
static
�� 
Models
�� 
.
�� 
Field
�� "
[
��" #
]
��# $ 
BuildFieldsForType
��% 7
(
��7 8
ISchemaProvider
��8 G
schema
��H N
,
��N O!
IReadOnlyDictionary
��P c
<
��c d
Type
��d h
,
��h i
string
��j p
>
��p q
combinedMapping��r �
,��� �
string��� �
typeName��� �
)��� �
{
�� 	
if
�� 
(
�� 
typeName
�� 
==
�� 
$str
�� #
)
��# $
{
�� 
return
�� "
BuildRootQueryFields
�� +
(
��+ ,
schema
��, 2
,
��2 3
combinedMapping
��4 C
)
��C D
;
��D E
}
�� 
if
�� 
(
�� 
typeName
�� 
==
�� 
$str
�� &
)
��& '
{
�� 
return
�� !
BuildMutationFields
�� *
(
��* +
schema
��+ 1
,
��1 2
combinedMapping
��3 B
)
��B C
;
��C D
}
�� 
var
�� 

fieldDescs
�� 
=
�� 
new
��  
List
��! %
<
��% &
Models
��& ,
.
��, -
Field
��- 2
>
��2 3
(
��3 4
)
��4 5
;
��5 6
if
�� 
(
�� 
!
�� 
schema
�� 
.
�� 
HasType
�� 
(
��  
typeName
��  (
)
��( )
)
��) *
{
�� 
return
�� 

fieldDescs
�� !
.
��! "
ToArray
��" )
(
��) *
)
��* +
;
��+ ,
}
�� 
var
�� 
type
�� 
=
�� 
schema
�� 
.
�� 
Type
�� "
(
��" #
typeName
��# +
)
��+ ,
;
��, -
foreach
�� 
(
�� 
var
�� 
field
�� 
in
�� !
type
��" &
.
��& '
	GetFields
��' 0
(
��0 1
)
��1 2
)
��2 3
{
�� 
if
�� 
(
�� 
field
�� 
.
�� 
Name
�� 
.
�� 

StartsWith
�� )
(
��) *
$str
��* .
)
��. /
)
��/ 0
continue
�� 
;
�� 

fieldDescs
�� 
.
�� 
Add
�� 
(
�� 
new
�� "
Models
��# )
.
��) *
Field
��* /
{
�� 
Args
�� 
=
�� 
	BuildArgs
�� $
(
��$ %
schema
��% +
,
��+ ,
combinedMapping
��- <
,
��< =
field
��> C
)
��C D
.
��D E
ToArray
��E L
(
��L M
)
��M N
,
��N O
DeprecationReason
�� %
=
��& '
$str
��( *
,
��* +
Description
�� 
=
��  !
field
��" '
.
��' (
Description
��( 3
,
��3 4
IsDeprecated
��  
=
��! "
false
��# (
,
��( )
Name
�� 
=
�� 
SchemaGenerator
�� *
.
��* +$
ToCamelCaseStartsLower
��+ A
(
��A B
field
��B G
.
��G H
Name
��H L
)
��L M
,
��M N
Type
�� 
=
�� 
	BuildType
�� $
(
��$ %
schema
��% +
,
��+ ,
field
��- 2
.
��2 3
ReturnTypeClr
��3 @
,
��@ A
field
��B G
.
��G H
ReturnTypeSingle
��H X
,
��X Y
combinedMapping
��Z i
)
��i j
,
��j k
}
�� 
)
�� 
;
�� 
}
�� 
return
�� 

fieldDescs
�� 
.
�� 
ToArray
�� %
(
��% &
)
��& '
;
��' (
}
�� 	
private
�� 
static
�� 
Models
�� 
.
�� 
Field
�� #
[
��# $
]
��$ %"
BuildRootQueryFields
��& :
(
��: ;
ISchemaProvider
��; J
schema
��K Q
,
��Q R!
IReadOnlyDictionary
��S f
<
��f g
Type
��g k
,
��k l
string
��m s
>
��s t
combinedMapping��u �
)��� �
{
�� 	
var
�� 

rootFields
�� 
=
�� 
new
��  
List
��! %
<
��% &
Models
��& ,
.
��, -
Field
��- 2
>
��2 3
(
��3 4
)
��4 5
;
��5 6
foreach
�� 
(
�� 
var
�� 
field
�� 
in
�� !
schema
��" (
.
��( )
GetQueryFields
��) 7
(
��7 8
)
��8 9
)
��9 :
{
�� 
if
�� 
(
�� 
field
�� 
.
�� 
Name
�� 
.
�� 

StartsWith
�� )
(
��) *
$str
��* .
)
��. /
)
��/ 0
continue
�� 
;
�� 
if
�� 
(
�� 
field
�� 
.
�� 
ReturnTypeClr
�� '
.
��' (
GetTypeInfo
��( 3
(
��3 4
)
��4 5
.
��5 6
IsEnum
��6 <
)
��< =
continue
�� 
;
�� 

rootFields
�� 
.
�� 
Add
�� 
(
�� 
new
�� "
Models
��# )
.
��) *
Field
��* /
{
�� 
Name
�� 
=
�� 
field
��  
.
��  !
Name
��! %
,
��% &
Args
�� 
=
�� 
	BuildArgs
�� $
(
��$ %
schema
��% +
,
��+ ,
combinedMapping
��- <
,
��< =
field
��> C
)
��C D
.
��D E
ToArray
��E L
(
��L M
)
��M N
,
��N O
IsDeprecated
��  
=
��! "
false
��# (
,
��( )
Type
�� 
=
�� 
	BuildType
�� $
(
��$ %
schema
��% +
,
��+ ,
field
��- 2
.
��2 3
ReturnTypeClr
��3 @
,
��@ A
field
��B G
.
��G H
ReturnTypeSingle
��H X
,
��X Y
combinedMapping
��Z i
)
��i j
,
��j k
Description
�� 
=
��  !
field
��" '
.
��' (
Description
��( 3
}
�� 
)
�� 
;
�� 
}
�� 
return
�� 

rootFields
�� 
.
�� 
ToArray
�� %
(
��% &
)
��& '
;
��' (
}
�� 	
private
�� 
static
�� 
Models
�� 
.
�� 
Field
�� #
[
��# $
]
��$ %!
BuildMutationFields
��& 9
(
��9 :
ISchemaProvider
��: I
schema
��J P
,
��P Q!
IReadOnlyDictionary
��R e
<
��e f
Type
��f j
,
��j k
string
��l r
>
��r s
combinedMapping��t �
)��� �
{
�� 	
var
�� 

rootFields
�� 
=
�� 
new
��  
List
��! %
<
��% &
Models
��& ,
.
��, -
Field
��- 2
>
��2 3
(
��3 4
)
��4 5
;
��5 6
foreach
�� 
(
�� 
var
�� 
field
�� 
in
�� !
schema
��" (
.
��( )
GetMutations
��) 5
(
��5 6
)
��6 7
)
��7 8
{
�� 
if
�� 
(
�� 
field
�� 
.
�� 
Name
�� 
.
�� 

StartsWith
�� )
(
��) *
$str
��* .
)
��. /
)
��/ 0
continue
�� 
;
�� 
if
�� 
(
�� 
field
�� 
.
�� 
ReturnTypeClr
�� '
.
��' (
GetTypeInfo
��( 3
(
��3 4
)
��4 5
.
��5 6
IsEnum
��6 <
)
��< =
continue
�� 
;
�� 
var
�� 
args
�� 
=
�� 
	BuildArgs
�� $
(
��$ %
schema
��% +
,
��+ ,
combinedMapping
��- <
,
��< =
field
��> C
)
��C D
.
��D E
ToArray
��E L
(
��L M
)
��M N
;
��N O

rootFields
�� 
.
�� 
Add
�� 
(
�� 
new
�� "
Models
��# )
.
��) *
Field
��* /
{
�� 
Name
�� 
=
�� 
field
��  
.
��  !
Name
��! %
,
��% &
Args
�� 
=
�� 
args
�� 
,
��  
IsDeprecated
��  
=
��! "
false
��# (
,
��( )
Type
�� 
=
�� 
	BuildType
�� $
(
��$ %
schema
��% +
,
��+ ,
field
��- 2
.
��2 3
ReturnTypeClr
��3 @
,
��@ A
field
��B G
.
��G H
ReturnTypeSingle
��H X
,
��X Y
combinedMapping
��Z i
)
��i j
,
��j k
Description
�� 
=
��  !
field
��" '
.
��' (
Description
��( 3
}
�� 
)
�� 
;
�� 
}
�� 
return
�� 

rootFields
�� 
.
�� 
ToArray
�� %
(
��% &
)
��& '
;
��' (
}
�� 	
private
�� 
static
�� 
List
�� 
<
�� 
Models
�� "
.
��" #

InputValue
��# -
>
��- .
	BuildArgs
��/ 8
(
��8 9
ISchemaProvider
��9 H
schema
��I O
,
��O P!
IReadOnlyDictionary
��Q d
<
��d e
Type
��e i
,
��i j
string
��k q
>
��q r
combinedMapping��s �
,��� �
IMethodType��� �
field��� �
)��� �
{
�� 	
var
�� 
args
�� 
=
�� 
new
�� 
List
�� 
<
��  
Models
��  &
.
��& '

InputValue
��' 1
>
��1 2
(
��2 3
)
��3 4
;
��4 5
foreach
�� 
(
�� 
var
�� 
arg
�� 
in
�� 
field
��  %
.
��% &
	Arguments
��& /
)
��/ 0
{
�� 
var
�� 
gqlTypeName
�� 
=
��  !
arg
��" %
.
��% &
Value
��& +
.
��+ ,!
IsEnumerableOrArray
��, ?
(
��? @
)
��@ A
?
��B C
arg
��D G
.
��G H
Value
��H M
.
��M N&
GetEnumerableOrArrayType
��N f
(
��f g
)
��g h
.
��h i
Name
��i m
:
��n o
arg
��p s
.
��s t
Value
��t y
.
��y z
Name
��z ~
;
��~ 
var
�� 
type
�� 
=
�� 
	BuildType
�� $
(
��$ %
schema
��% +
,
��+ ,
arg
��- 0
.
��0 1
Value
��1 6
,
��6 7
gqlTypeName
��8 C
,
��C D
combinedMapping
��E T
)
��T U
;
��U V
args
�� 
.
�� 
Add
�� 
(
�� 
new
�� 
Models
�� #
.
��# $

InputValue
��$ .
{
�� 
Name
�� 
=
�� 
arg
�� 
.
�� 
Key
�� "
,
��" #
Type
�� 
=
�� 
type
�� 
,
��  
DefaultValue
��  
=
��! "
null
��# '
,
��' (
Description
�� 
=
��  !
null
��" &
,
��& '
}
�� 
)
�� 
;
�� 
}
�� 
return
�� 
args
�� 
;
�� 
}
�� 	
private
�� 
static
�� 
string
�� 
FindNamedMapping
�� .
(
��. /
Type
��/ 3
name
��4 8
,
��8 9!
IReadOnlyDictionary
��: M
<
��M N
Type
��N R
,
��R S
string
��T Z
>
��Z [
combinedMapping
��\ k
,
��k l
string
��m s
fallback
��t |
=
��} ~
null�� �
)��� �
{
�� 	
if
�� 
(
�� 
combinedMapping
�� 
.
��  
Any
��  #
(
��# $
x
��$ %
=>
��& (
x
��) *
.
��* +
Key
��+ .
==
��/ 1
name
��2 6
)
��6 7
)
��7 8
return
�� 
combinedMapping
�� &
[
��& '
name
��' +
]
��+ ,
;
��, -
else
�� 
if
�� 
(
�� 
string
�� 
.
�� 
IsNullOrEmpty
�� (
(
��( )
fallback
��) 1
)
��1 2
)
��2 3
return
�� 
name
�� 
.
��  
Name
��  $
;
��$ %
else
�� 
return
�� 
fallback
�� #
;
��# $
}
�� 	
private
�� 
static
�� 
List
�� 
<
�� 
Models
�� "
.
��" #

Directives
��# -
>
��- .
BuildDirectives
��/ >
(
��> ?
)
��? @
{
�� 	
var
�� 

directives
�� 
=
�� 
new
��  
List
��! %
<
��% &
Models
��& ,
.
��, -

Directives
��- 7
>
��7 8
{
��9 :
}
�� 
;
�� 
return
�� 

directives
�� 
;
�� 
}
�� 	
}
�� 
}�� ��
?Y:\Develop\EntityGraphQL\src\EntityGraphQL\Schema\SchemaType.cs
	namespace		 	
EntityGraphQL		
 
.		 
Schema		 
{

 
public 

class 

SchemaType 
< 
	TBaseType %
>% &
:' (
ISchemaType) 4
{ 
public 
Type 
ContextType 
{  !
get" %
;% &
	protected' 0
set1 4
;4 5
}6 7
public 
string 
Name 
{ 
get  
;  !
	protected" +
set, /
;/ 0
}1 2
public 
bool 
IsInput 
{ 
get !
;! "
}# $
public 
string 
Description !
=>" $
_description% 1
;1 2
private 
string 
_description #
;# $
private 

Dictionary 
< 
string !
,! "
Field# (
>( )
_fieldsByName* 7
=8 9
new: =

Dictionary> H
<H I
stringI O
,O P
FieldQ V
>V W
(W X
)X Y
;Y Z
private 
readonly 

Expression #
<# $
Func$ (
<( )
	TBaseType) 2
,2 3
bool4 8
>8 9
>9 :
_filter; B
;B C
public 

SchemaType 
( 
string  
name! %
,% &
string' -
description. 9
,9 :

Expression; E
<E F
FuncF J
<J K
	TBaseTypeK T
,T U
boolV Z
>Z [
>[ \
filter] c
=d e
nullf j
,j k
booll p
isInputq x
=y z
false	{ �
)
� �
:
� �
this
� �
(
� �
typeof
� �
(
� �
	TBaseType
� �
)
� �
,
� �
name
� �
,
� �
description
� �
,
� �
filter
� �
,
� �
isInput
� �
)
� �
{ 	
} 	
public 

SchemaType 
( 
Type 
contextType *
,* +
string, 2
name3 7
,7 8
string9 ?
description@ K
,K L

ExpressionM W
<W X
FuncX \
<\ ]
	TBaseType] f
,f g
boolh l
>l m
>m n
filtero u
=v w
nullx |
,| }
bool	~ �
isInput
� �
=
� �
false
� �
)
� �
{ 	
ContextType 
= 
contextType %
;% &
Name 
= 
name 
; 
_description 
= 
description &
;& '
_filter   
=   
filter   
;   
IsInput!! 
=!! 
isInput!! 
;!! 
AddField"" 
("" 
$str"" !
,""! "
t""# $
=>""% '
name""( ,
,"", -
$str"". 9
)""9 :
;"": ;
}## 	
public(( 

SchemaType(( 
<(( 
	TBaseType(( #
>((# $
AddAllFields((% 1
(((1 2
)((2 3
{)) 	
BuildFieldsFromBase** 
(**  
typeof**  &
(**& '
	TBaseType**' 0
)**0 1
)**1 2
;**2 3
return++ 
this++ 
;++ 
},, 	
public-- 
void-- 
	AddFields-- 
(-- 
List-- "
<--" #
Field--# (
>--( )
fields--* 0
)--0 1
{.. 	
foreach// 
(// 
var// 
f// 
in// 
fields// $
)//$ %
{00 
AddField11 
(11 
f11 
)11 
;11 
}22 
}33 	
public;; 
void;; 
AddField;; 
<;; 
TReturn;; $
>;;$ %
(;;% &

Expression;;& 0
<;;0 1
Func;;1 5
<;;5 6
	TBaseType;;6 ?
,;;? @
TReturn;;A H
>;;H I
>;;I J
fieldSelection;;K Y
,;;Y Z
string;;[ a
description;;b m
,;;m n
string;;o u
returnSchemaType	;;v �
=
;;� �
null
;;� �
)
;;� �
{<< 	
var== 
exp== 
=== 
ExpressionUtil== $
.==$ %'
CheckAndGetMemberExpression==% @
(==@ A
fieldSelection==A O
)==O P
;==P Q
AddField>> 
(>> 
SchemaGenerator>> $
.>>$ %"
ToCamelCaseStartsLower>>% ;
(>>; <
exp>>< ?
.>>? @
Member>>@ F
.>>F G
Name>>G K
)>>K L
,>>L M
fieldSelection>>N \
,>>\ ]
description>>^ i
,>>i j
returnSchemaType>>k {
)>>{ |
;>>| }
}?? 	
public@@ 
void@@ 
AddField@@ 
(@@ 
Field@@ "
field@@# (
)@@( )
{AA 	
ifBB 
(BB 
_fieldsByNameBB 
.BB 
ContainsKeyBB )
(BB) *
fieldBB* /
.BB/ 0
NameBB0 4
)BB4 5
)BB5 6
throwCC 
newCC "
EntityQuerySchemaErrorCC 0
(CC0 1
$"CC1 3
Field CC3 9
{CC9 :
fieldCC: ?
.CC? @
NameCC@ D
}CCD E$
 already exists on type CCE ]
{CC] ^
thisCC^ b
.CCb c
NameCCc g
}CCg h6
). Use ReplaceField() if this is intended.	CCh �
"
CC� �
)
CC� �
;
CC� �
_fieldsByNameEE 
.EE 
AddEE 
(EE 
fieldEE #
.EE# $
NameEE$ (
,EE( )
fieldEE* /
)EE/ 0
;EE0 1
ifFF 
(FF 
!FF 
_fieldsByNameFF 
.FF 
ContainsKeyFF *
(FF* +
fieldFF+ 0
.FF0 1
NameFF1 5
)FF5 6
)FF6 7
_fieldsByNameGG 
.GG 
AddGG !
(GG! "
fieldGG" '
.GG' (
NameGG( ,
,GG, -
fieldGG. 3
)GG3 4
;GG4 5
}HH 	
publicII 
voidII 
AddFieldII 
<II 
TReturnII $
>II$ %
(II% &
stringII& ,
nameII- 1
,II1 2

ExpressionII3 =
<II= >
FuncII> B
<IIB C
	TBaseTypeIIC L
,IIL M
TReturnIIN U
>IIU V
>IIV W
fieldSelectionIIX f
,IIf g
stringIIh n
descriptionIIo z
,IIz {
string	II| �
returnSchemaType
II� �
=
II� �
null
II� �
)
II� �
{JJ 	
varKK 
fieldKK 
=KK 
newKK 
FieldKK !
(KK! "
nameKK" &
,KK& '
fieldSelectionKK( 6
,KK6 7
descriptionKK8 C
,KKC D
returnSchemaTypeKKE U
)KKU V
;KKV W
thisLL 
.LL 
AddFieldLL 
(LL 
fieldLL 
)LL  
;LL  !
}MM 	
publicNN 
voidNN 
ReplaceFieldNN  
<NN  !
TReturnNN! (
>NN( )
(NN) *
stringNN* 0
nameNN1 5
,NN5 6

ExpressionNN7 A
<NNA B
FuncNNB F
<NNF G
	TBaseTypeNNG P
,NNP Q
TReturnNNR Y
>NNY Z
>NNZ [
selectionExpressionNN\ o
,NNo p
stringNNq w
description	NNx �
,
NN� �
string
NN� �
returnSchemaType
NN� �
=
NN� �
null
NN� �
)
NN� �
{OO 	
varPP 
fieldPP 
=PP 
newPP 
FieldPP !
(PP! "
namePP" &
,PP& '
selectionExpressionPP( ;
,PP; <
descriptionPP= H
,PPH I
returnSchemaTypePPJ Z
)PPZ [
;PP[ \
_fieldsByNameQQ 
[QQ 
fieldQQ 
.QQ  
NameQQ  $
]QQ$ %
=QQ& '
fieldQQ( -
;QQ- .
}RR 	
public__ 
void__ 
AddField__ 
<__ 
TParams__ $
,__$ %
TReturn__& -
>__- .
(__. /
string__/ 5
name__6 :
,__: ;
TParams__< C
argTypes__D L
,__L M

Expression__N X
<__X Y
Func__Y ]
<__] ^
	TBaseType__^ g
,__g h
TParams__i p
,__p q
TReturn__r y
>__y z
>__z { 
selectionExpression	__| �
,
__� �
string
__� �
description
__� �
,
__� �
string
__� �
returnSchemaType
__� �
=
__� �
null
__� �
)
__� �
{`` 	
varaa 
fieldaa 
=aa 
newaa 
Fieldaa !
(aa! "
nameaa" &
,aa& '
selectionExpressionaa( ;
,aa; <
descriptionaa= H
,aaH I
returnSchemaTypeaaJ Z
,aaZ [
argTypesaa\ d
)aad e
;aae f
thisbb 
.bb 
AddFieldbb 
(bb 
fieldbb 
)bb  
;bb  !
}cc 	
publicpp 
voidpp 
ReplaceFieldpp  
<pp  !
TParamspp! (
,pp( )
TReturnpp* 1
>pp1 2
(pp2 3
stringpp3 9
namepp: >
,pp> ?
TParamspp@ G
argTypesppH P
,ppP Q

ExpressionppR \
<pp\ ]
Funcpp] a
<ppa b
	TBaseTypeppb k
,ppk l
TParamsppm t
,ppt u
TReturnppv }
>pp} ~
>pp~ !
selectionExpression
pp� �
,
pp� �
string
pp� �
description
pp� �
,
pp� �
string
pp� �
returnSchemaType
pp� �
=
pp� �
null
pp� �
)
pp� �
{qq 	
varrr 
fieldrr 
=rr 
newrr 
Fieldrr !
(rr! "
namerr" &
,rr& '
selectionExpressionrr( ;
,rr; <
descriptionrr= H
,rrH I
returnSchemaTyperrJ Z
,rrZ [
argTypesrr\ d
)rrd e
;rre f
_fieldsByNamess 
[ss 
fieldss 
.ss  
Namess  $
]ss$ %
=ss& '
fieldss( -
;ss- .
}tt 	
privatevv 
voidvv 
BuildFieldsFromBasevv (
(vv( )
Typevv) -
contextTypevv. 9
)vv9 :
{ww 	
foreachxx 
(xx 
varxx 
fxx 
inxx 
ContextTypexx )
.xx) *
GetPropertiesxx* 7
(xx7 8
)xx8 9
)xx9 :
{yy 
ifzz 
(zz 
!zz 
_fieldsByNamezz "
.zz" #
ContainsKeyzz# .
(zz. /
fzz/ 0
.zz0 1
Namezz1 5
)zz5 6
)zz6 7
{{{ 
string}} 
description}} &
=}}' (
string}}) /
.}}/ 0
Empty}}0 5
;}}5 6
var~~ 
d~~ 
=~~ 
(~~ 
System~~ #
.~~# $
ComponentModel~~$ 2
.~~2 3 
DescriptionAttribute~~3 G
)~~G H
f~~H I
.~~I J
GetCustomAttribute~~J \
(~~\ ]
typeof~~] c
(~~c d
System~~d j
.~~j k
ComponentModel~~k y
.~~y z!
DescriptionAttribute	~~z �
)
~~� �
,
~~� �
false
~~� �
)
~~� �
;
~~� �
if 
( 
d 
!= 
null !
)! "
description
�� #
=
��$ %
d
��& '
.
��' (
Description
��( 3
;
��3 4
var
�� 
	parameter
�� !
=
��" #

Expression
��$ .
.
��. /
	Parameter
��/ 8
(
��8 9
ContextType
��9 D
)
��D E
;
��E F
this
�� 
.
�� 
AddField
�� !
(
��! "
new
��" %
Field
��& +
(
��+ ,
SchemaGenerator
��, ;
.
��; <$
ToCamelCaseStartsLower
��< R
(
��R S
f
��S T
.
��T U
Name
��U Y
)
��Y Z
,
��Z [

Expression
��\ f
.
��f g
Lambda
��g m
(
��m n

Expression
��n x
.
��x y
Property��y �
(��� �
	parameter��� �
,��� �
f��� �
.��� �
Name��� �
)��� �
,��� �
	parameter��� �
)��� �
,��� �
description��� �
,��� �
null��� �
)��� �
)��� �
;��� �
}
�� 
}
�� 
foreach
�� 
(
�� 
var
�� 
f
�� 
in
�� 
ContextType
�� )
.
��) *
	GetFields
��* 3
(
��3 4
)
��4 5
)
��5 6
{
�� 
if
�� 
(
�� 
!
�� 
_fieldsByName
�� "
.
��" #
ContainsKey
��# .
(
��. /
f
��/ 0
.
��0 1
Name
��1 5
)
��5 6
)
��6 7
{
�� 
string
�� 
description
�� &
=
��' (
string
��) /
.
��/ 0
Empty
��0 5
;
��5 6
var
�� 
d
�� 
=
�� 
(
�� 
System
�� #
.
��# $
ComponentModel
��$ 2
.
��2 3"
DescriptionAttribute
��3 G
)
��G H
f
��H I
.
��I J 
GetCustomAttribute
��J \
(
��\ ]
typeof
��] c
(
��c d
System
��d j
.
��j k
ComponentModel
��k y
.
��y z#
DescriptionAttribute��z �
)��� �
,��� �
false��� �
)��� �
;��� �
if
�� 
(
�� 
d
�� 
!=
�� 
null
�� !
)
��! "
description
�� #
=
��$ %
d
��& '
.
��' (
Description
��( 3
;
��3 4
var
�� 
	parameter
�� !
=
��" #

Expression
��$ .
.
��. /
	Parameter
��/ 8
(
��8 9
ContextType
��9 D
)
��D E
;
��E F
this
�� 
.
�� 
AddField
�� !
(
��! "
new
��" %
Field
��& +
(
��+ ,
SchemaGenerator
��, ;
.
��; <$
ToCamelCaseStartsLower
��< R
(
��R S
f
��S T
.
��T U
Name
��U Y
)
��Y Z
,
��Z [

Expression
��\ f
.
��f g
Lambda
��g m
(
��m n

Expression
��n x
.
��x y
Field
��y ~
(
��~ 
	parameter�� �
,��� �
f��� �
.��� �
Name��� �
)��� �
,��� �
	parameter��� �
)��� �
,��� �
description��� �
,��� �
null��� �
)��� �
)��� �
;��� �
}
�� 
}
�� 
}
�� 	
public
�� 
Field
�� 
GetField
�� 
(
�� 
string
�� $

identifier
��% /
)
��/ 0
{
�� 	
if
�� 
(
�� 
_fieldsByName
�� 
.
�� 
ContainsKey
�� )
(
��) *

identifier
��* 4
)
��4 5
)
��5 6
return
�� 
_fieldsByName
�� $
[
��$ %

identifier
��% /
]
��/ 0
;
��0 1
throw
�� 
new
�� ,
EntityGraphQLCompilerException
�� 4
(
��4 5
$"
��5 7
Field 
��7 =
{
��= >

identifier
��> H
}
��H I

 not found
��I S
"
��S T
)
��T U
;
��U V
}
�� 	
public
�� 
IEnumerable
�� 
<
�� 
Field
��  
>
��  !
	GetFields
��" +
(
��+ ,
)
��, -
{
�� 	
return
�� 
_fieldsByName
��  
.
��  !
Values
��! '
;
��' (
}
�� 	
public
�� 
bool
�� 
HasField
�� 
(
�� 
string
�� #

identifier
��$ .
)
��. /
{
�� 	
return
�� 
_fieldsByName
��  
.
��  !
ContainsKey
��! ,
(
��, -

identifier
��- 7
)
��7 8
;
��8 9
}
�� 	
public
�� 
void
�� 
RemoveField
�� 
(
��  
string
��  &
name
��' +
)
��+ ,
{
�� 	
if
�� 
(
�� 
_fieldsByName
�� 
.
�� 
ContainsKey
�� )
(
��) *
name
��* .
)
��. /
)
��/ 0
{
�� 
_fieldsByName
�� 
.
�� 
Remove
�� $
(
��$ %
name
��% )
)
��) *
;
��* +
}
�� 
}
�� 	
public
�� 
void
�� 
RemoveField
�� 
(
��  

Expression
��  *
<
��* +
Func
��+ /
<
��/ 0
	TBaseType
��0 9
,
��9 :
object
��; A
>
��A B
>
��B C
fieldSelection
��D R
)
��R S
{
�� 	
var
�� 
exp
�� 
=
�� 
ExpressionUtil
�� $
.
��$ %)
CheckAndGetMemberExpression
��% @
(
��@ A
fieldSelection
��A O
)
��O P
;
��P Q
RemoveField
�� 
(
�� 
SchemaGenerator
�� '
.
��' ($
ToCamelCaseStartsLower
��( >
(
��> ?
exp
��? B
.
��B C
Member
��C I
.
��I J
Name
��J N
)
��N O
)
��O P
;
��P Q
}
�� 	
}
�� 
}�� 