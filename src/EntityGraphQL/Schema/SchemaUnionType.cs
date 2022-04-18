// using System;
// using System.Collections.Generic;

// namespace EntityGraphQL.Schema;

// public class SchemaUnionType : ISchemaType
// {
//     public Type TypeDotnet => throw new NotImplementedException();

//     public string Name { get; set; }

//     public string? Description { get; set; }

//     public bool IsInput => false;

//     public bool IsEnum => false;

//     public bool IsScalar => false;

//     public RequiredAuthorization? RequiredAuthorization { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

//     public ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true)
//     {
//         throw new NotImplementedException();
//     }

//     public IField AddField(IField field)
//     {
//         throw new NotImplementedException();
//     }

//     public void AddFields(IEnumerable<IField> fields)
//     {
//         throw new NotImplementedException();
//     }

//     public IField GetField(string identifier, QueryRequestContext? requestContext)
//     {
//         throw new NotImplementedException();
//     }

//     public IEnumerable<IField> GetFields()
//     {
//         throw new NotImplementedException();
//     }

//     public bool HasField(string identifier, QueryRequestContext? requestContext)
//     {
//         throw new NotImplementedException();
//     }

//     public void RemoveField(string name)
//     {
//         throw new NotImplementedException();
//     }
// }