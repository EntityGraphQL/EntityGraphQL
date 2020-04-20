grammar EntityGraphQL;

// Core building blocks
ID          : [a-z_A-Z]+[a-z_A-Z0-9-]*;
DIGIT       : [0-9];
STRING_CHARS: [a-zA-Z0-9 \t`~!@#$%^&*()_+={}|\\:\"'\u005B\u005D;<>?,./-];

identity    : ID;
int         : '-'? DIGIT+;
decimal     : '-'? DIGIT+'.'DIGIT+;
boolean     : 'true' | 'false';
string      : '"' ( '"' | ~('\n'|'\r') | STRING_CHARS )*? '"';
null        : 'null';
constant    : string | int | decimal | boolean | null;

ws          : ' ' | '\t' | '\n' | '\r';

// Core building blocks for parsing GQL
varArray    : '[' type=identity required='!'? ']';
wsc         : ' ' | '\t' | '\n' | '\r' | comment;

// this is a data query (graphQL inspired)
// # my comment
// query {
//   entity1 { field1 field2 relation { field1 field2 } }
//   entity2 { field1 field2 relation { field1 field2 } }
// }
queryKeyword        : 'query';
mutationKeyword     : 'mutation';
subscriptionKeyword : 'subscription';

gqlCall     : '(' ws* gqlarguments=gqlargs ws* ')';
gqlargs     : gqlarg (ws* ',' ws* gqlarg)*;
gqlTypeDefs : gqlTypeDef (',' ws* gqlTypeDef)*;
gqlTypeDef  : gqlVar ws* ':' ws* (type=identity | arrayType=varArray) required='!'? (ws* '=' ws* defaultValue=constant)?;
gqlVar      : '$' identity;
gqlarg      : gqlfield=identity ws* ':' ws* (gqlvalue=constant | gqlvar=gqlVar);

directiveCall       : ws* '@' name=identity (ws* '(' ws* directiveArgs=gqlargs ws* ')')? ws*;
aliasType           : name=identity ws* ':' ws*;
field               : alias=aliasType? fieldDef=identity argsCall=gqlCall? ws* directive=directiveCall? ws* select=objectSelection?;
fragmentSelect      : '...' name=identity;
objectSelection     : ws* '{' wsc* (field | fragmentSelect) ((ws* ','? wsc*) (field | fragmentSelect) wsc*)* '}' ws*;
operationName       : operation=identity ('(' (operationArgs=gqlTypeDefs)? ')')?;
gqlBody             : '{' wsc* field (ws* ','? wsc* field)* wsc* '}';
dataQuery           : wsc* (queryKeyword | (queryKeyword ws* operationName))? ws* gqlBody wsc*;
mutationQuery       : wsc* mutationKeyword ws* operationName ws* gqlBody wsc*;
subscriptionQuery   : wsc* subscriptionKeyword ws* operationName ws* gqlBody wsc*;
comment             : '#' ~( '\r' | '\n' | EOF )* ( '\r' | '\n' | EOF );
gqlFragment         : wsc* 'fragment' ws+ fragmentName=identity ws+ 'on' ws+ fragmentType=identity ws* fields=objectSelection wsc*;

graphQL             : ( gqlFragment* (dataQuery | mutationQuery | subscriptionQuery) gqlFragment* )+;

// This is EntityQuery expression language
args        : expression (',' ws* expression)*;
call        : method=identity '(' arguments=args? ')';
callPath    : (identity | call) ('.' (identity | call))*;
operator    : '-' | '+' | '%' | '^' | 'and' | '*' | 'or' | '=' | '<=' | '>=' | '<' | '>' | '/';

expression  : 'if ' (' ' | '\t')* test=expression (' ' | '\t')* 'then ' (' ' | '\t')* ifTrue=expression (' ' | '\t')* 'else ' (' ' | '\t')* ifFalse=expression #ifThenElse
              | test=expression ' '* '?' ' '* ifTrue=expression ' '* ':' ' '* ifFalse=expression #ifThenElseInline
              | left=expression ' '* op=operator ' '* right=expression #binary
              | '(' body=expression ')' #expr
              | constant #const
              | callPath #callOrId;

eqlStart    : expression;