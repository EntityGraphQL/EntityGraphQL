grammar EntityQL;

// Core building blocks
ID: [a-z_A-Z]+ [a-z_A-Z0-9-]*;
DIGIT: [0-9];
fragment ESCAPE_CHARS: '\\\'' | '\\"' | '\\\\' | '\\0' | '\\a' | '\\b' | '\\f' | '\\n' | '\\r' | '\\t' | '\\v';
// identity includes keywords too
identity: ID;

int: '-'? DIGIT+;
decimal: '-'? DIGIT+ '.' DIGIT+;
boolean: 'true' | 'false';
STRING: '"' (~['"'\\\r\n\u0085\u2028\u2029] | ESCAPE_CHARS)* '"';
null: 'null';
constant: stringVal = STRING
	| int
	| decimal
	| boolean
	| null
	| identity; // identity should end up being an enum

ws: ' ' | '\t' | '\n' | '\r';

args: expression (',' ws* expression)*;
call: method = identity '(' arguments = args? ')';
callPath: (identity | call) ('.' (identity | call))*;

expression: 
	'if ' (' ' | '\t')* test = expression (' ' | '\t')* 'then ' (' ' | '\t')* ifTrue = expression (' ' | '\t')* 'else ' (' ' | '\t')* ifFalse = expression # ifThenElse
	| test = expression ' '* '?' ' '* ifTrue = expression ' '* ':' ' '* ifFalse = expression #ifThenElseInline
	| left = expression ' '* op = ('*' | '/' | '%') ' '* right = expression # binary
	| left = expression ' '* op = ('+' | '-') ' '* right = expression # binary
	| left = expression ' '* op = ('<=' | '>=' | '<' | '>') ' '* right = expression # binary
	| left = expression ' '* op = ('==' | '!=') ' '* right = expression # binary
	| left = expression ' '* op = '^' ' '* right = expression # binary
	| left = expression ' '* op = ('and' | '&&') ' '* right = expression # logic
	| left = expression ' '* op = ('or' | '||') ' '* right = expression # logic
	| '(' body = expression ')' # expr
	| callPath # callOrId
	| constant # const
	;
	 
eqlStart: expr = expression EOF;