grammar EntityQL;

// Core building blocks
ID: [a-z_A-Z]+ [a-z_A-Z0-9-]*;
DIGIT: [0-9];
STRING_CHARS: [a-zA-Z0-9 \t`~!@#$%^&*()_+={}|\\:"'\u005B\u005D;<>?,./-];

// identity includes keywords too
identity: ID;

int: '-'? DIGIT+;
decimal: '-'? DIGIT+ '.' DIGIT+;
boolean: 'true' | 'false';
string: '"' ( '"' | ~('\n' | '\r') | STRING_CHARS)*? '"';
null: 'null';
constant: string
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