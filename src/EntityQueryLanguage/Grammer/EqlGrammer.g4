grammar EqlGrammer;

// This is our expression language
ID          : [a-z_A-Z]+[a-z_A-Z\w\-0-9]*;
DIGIT       : [0-9];
STRING_CHARS: [ \t`~!@#$%^&*\(\)_+\-={}|\[\]:\";<>?,\./];

identity    : ID;
callPath    : (identity | call) ('.' (identity | call))*;
int         : '-'? DIGIT+;
decimal     : '-'? DIGIT+'.'DIGIT+;
string      :   '\'' ( '\'' | ~('\n'|'\r') | STRING_CHARS )*? '\'';
constant    : string | int | decimal;
call        : method=identity '(' arguments=args? ')';
args        : expression (',' expression)*;

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
//   entity1 { field1, field2, relation { field1, field2 } },
//   entity2 { field1, field2, relation { field1, field2 } }
// }
ws          : (' ' | '\t' | '\n' | '\r');
field       : callPath;
aliasExp    : name=identity ws* ':' ws* entity=expression;
fieldSelect : '{' ws* (aliasExp | field | entityQuery) (ws* ',' ws* (aliasExp | field | entityQuery))* ws* '}';
entityQuery : (alias=aliasExp | entity=callPath) ws* fields=fieldSelect ws*;
dataQuery   : ws* '{' ws* entityQuery ( ws* ',' ws* entityQuery)* ws* '}' ws*;
