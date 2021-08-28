grammar EntityQL;

// Core building blocks
ID: [a-z_A-Z]+ [a-z_A-Z0-9-]*;
DIGIT: [0-9];
STRING_CHARS: [a-zA-Z0-9 \t`~!@#$%^&*()_+={}|\\:\"'\u005B\u005D;<>?,./-];

// identity includes keywords too
identity: ID
	| 'true'
	| 'false'
	| 'and'
	| 'if'
	| 'then'
	| 'else';

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

// This is EntityQuery expression language
args: expression (',' ws* expression)*;
call: method = identity '(' arguments = args? ')';
callPath: (identity | call) ('.' (identity | call))*;
operator: '-'
	| '+'
	| '%'
	| '^'
	| '*'
	| '=='
	| '<='
	| '>='
	| '<'
	| '>'
	| '/'
	| 'or'
	| '||'
	| 'and'
	| '&&';

expression:
	'if ' (' ' | '\t')* test = expression (' ' | '\t')* 'then ' (' ' | '\t')* ifTrue = expression (' ' | '\t')* 'else ' (' ' | '\t')* ifFalse = expression # ifThenElse
	| test = expression ' '* '?' ' '* ifTrue = expression ' '* ':' ' '* ifFalse = expression #ifThenElseInline
	| left = expression ' '* op = operator ' '* right = expression	# binary
	| '(' body = expression ')'										# expr
	| callPath														# callOrId
	| constant														# const;

eqlStart: expression;