grammar EqlGrammer;

// This is our expression language
ID          : [a-z_A-Z]+[a-z_A-Z0-9-]*;
DIGIT       : [0-9];
STRING_CHARS: [a-zA-Z0-9 \t`~!@#$%^&*()_+={}|\\:\"\u005B\u005D;<>?,./-];

identity    : ID;
callPath    : (identity | call | gqlcall) ('.' (identity | call | gqlcall))*;
int         : '-'? DIGIT+;
decimal     : '-'? DIGIT+'.'DIGIT+;
string      :   '\'' ( '\'' | ~('\n'|'\r') | STRING_CHARS )*? '\'';
constant    : string | int | decimal;
call        : method=identity '(' arguments=args? ')';
gqlcall     : method=identity '(' gqlarguments=gqlargs ')';
args        : expression (',' expression)*;
gqlargs     : gqlarg (',' gqlarg)*;
gqlarg      : gqlfield=identity ws* ':' ws* gqlvalue=expression;

operator    : '-' | '+' | '%' | '^' | 'and' | '*' | 'or' | '=' | '<=' | '>=' | '<' | '>' | '/';

expression  : 'if' ' '* test=expression ' '* 'then' ' '* ifTrue=expression ' '* 'else' ' '* ifFalse=expression #ifThenElse
              | test=expression ' '* '?' ' '* ifTrue=expression ' '* ':' ' '* ifFalse=expression #ifThenElseInline
              | left=expression ' '* op=operator ' '* right=expression #binary
              | '(' body=expression ')' #expr
              | callPath #callOrId
              | constant #const;

startRule   : expression;

// this is a data query (graphQL inspired)
// {
//   entity1 { field1 field2 relation { field1 field2 } }
//   entity2 { field1 field2 relation { field1 field2 } }
// }
ws          : (' ' | '\t' | '\n' | '\r');
queryKeyword: 'query';
field       : callPath;
aliasType   : name=identity ws* ':' ws*;
aliasExp    : alias=aliasType entity=expression;
fieldSelect : '{' ws* (aliasExp | field | entityQuery) ((ws* ','? ws*) (aliasExp | field | entityQuery))* ws* '}';
entityQuery : alias=aliasType? entity=callPath ws* fields=fieldSelect ws*;
dataQuery   : queryKeyword? ws* '{' ws* (aliasExp | entityQuery) ( (ws* ','? ws*) (aliasExp | entityQuery))* ws* '}' ws*;
