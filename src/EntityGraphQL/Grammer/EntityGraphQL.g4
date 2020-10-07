grammar EntityGraphQL;

// Core building blocks
ID: [a-z_A-Z]+ [a-z_A-Z0-9-]*;
DIGIT: [0-9];
STRING_CHARS: [a-zA-Z0-9 \t`~!@#$%^&*()_+={}|\\:\"'\u005B\u005D;<>?,./-];

// identity includes keywords too
identity: ID
	| 'true'
	| 'false'
	| 'query'
	| 'mutation'
	| 'subscription'
	| 'fragment'
	| 'on'
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

// Core building blocks for parsing GQL
varArray: '[' type = identity required = '!'? ']';
wsc: comment | ws;

// this is a data query (graphQL inspired) # my comment query { entity1 { field1 field2 relation {
// field1 field2 } } entity2 { field1 field2 relation { field1 field2 } } }
queryKeyword: 'query';
mutationKeyword: 'mutation';
subscriptionKeyword: 'subscription';

gqlCall: '(' ws* gqlarguments = gqlargs ws* ')';
gqlargs: gqlarg (ws* ','? ws* gqlarg)*;
gqlTypeDefs: gqlTypeDef (','? ws* gqlTypeDef)*;
gqlTypeDef:
	gqlVar ws* ':' ws* (type = identity | arrayType = varArray) required = '!'? (
		ws* '=' ws* defaultValue = constant
	)?;
gqlVar: '$' identity;
gqlarg:
	gqlfield = identity ws* ':' ws* (
		gqlvalue = constant
		| gqlvar = gqlVar
	);

directiveCall: ws* '@' name = identity (ws* '(' ws* directiveArgs = gqlargs ws* ')')? ws*;
aliasType: name = identity ws* ':' ws*;
field: alias = aliasType? fieldDef = identity argsCall = gqlCall? ws* directive = directiveCall? ws* select = objectSelection?;
fragmentSelect: '...' name = identity;
objectSelection:
	ws* '{' wsc* (field | fragmentSelect) (
		(ws* ','? wsc*) (field | fragmentSelect) wsc*
	)* wsc* '}' wsc*;
operationName: operation = identity ('(' (operationArgs = gqlTypeDefs)? ')')?;
dataQuery: wsc* (queryKeyword | (queryKeyword ws* operationName))? ws* objectSelection;
mutationQuery: wsc* mutationKeyword ws* operationName? ws* objectSelection;
subscriptionQuery: wsc* subscriptionKeyword ws* operationName? ws* objectSelection;
gqlFragment: wsc* 'fragment' ws+ fragmentName = identity ws+ 'on' ws+ fragmentType = identity ws* fields=objectSelection;

comment: ws* (singleLineDoc | multiLineDoc | ignoreComment) ws*;
ignoreComment: '#' ~('\n' | '\r')* ('\n' | '\r' | EOF);
multiLineDoc: '"""' ~'"""'* '"""';
singleLineDoc: '"' ~('\n' | '\r')* '"';

graphQL: gqlFragment* (dataQuery | mutationQuery | subscriptionQuery) (
		dataQuery
		| mutationQuery
		| subscriptionQuery
		| gqlFragment
	)*;

// This is EntityQuery expression language
args: expression (',' ws* expression)*;
call: method = identity '(' arguments = args? ')';
callPath: (identity | call) ('.' (identity | call))*;
operator: '-'
	| '+'
	| '%'
	| '^'
	| 'and'
	| '*'
	| 'or'
	| '='
	| '<='
	| '>='
	| '<'
	| '>'
	| '/';

expression:
	'if ' (' ' | '\t')* test = expression (' ' | '\t')* 'then ' (' ' | '\t')* ifTrue = expression (' ' | '\t')* 'else ' (' ' | '\t')* ifFalse = expression # ifThenElse
	| test = expression ' '* '?' ' '* ifTrue = expression ' '* ':' ' '* ifFalse = expression #ifThenElseInline
	| left = expression ' '* op = operator ' '* right = expression	# binary
	| '(' body = expression ')'										# expr
	| callPath														# callOrId
	| constant														# const;

eqlStart: expression;