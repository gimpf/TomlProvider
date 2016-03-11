namespace Gimpf.Toml
// #nowarn "25" // incomplete pattern matches
open System
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open Gimpf.FSharpExt

module internal TypeMapper =
    type TomlValueType =
        | TomlBool
        | TomlInt
        | TomlFloat
        | TomlString
        | TomlDate
        | TomlTimespan
        override x.ToString() =
            match x with
            | TomlBool -> "bool"
            | TomlInt -> "int"
            | TomlFloat -> "float"
            | TomlString -> "string"
            | TomlDate -> "DateTime"
            | TomlTimespan -> "Timespan"

    type TomlType =
        | TomlObject
        | TomlValueType of TomlValueType
        | TomlArray of TomlType
        | TomlTable of Map<string, TomlType>
        | TomlTableArray of TomlType // TODO should only allow tables
        override x.ToString() =
            match x with
            | TomlObject       -> "TomlObject"
            | TomlValueType x  -> x.ToString()
            | TomlArray x      -> "TomlArray,{" + x.ToString() + "}"
            | TomlTable x      -> "TomlTable,{"+(x |> Map.toSeq |> Seq.map (fun (n,t) -> sprintf "%s:%O" n t) |> String.concat ",")+"}"
            | TomlTableArray x -> "TomlTableArray,{" + x.ToString() + "}"

    /// Extract the complete type-structure from a TOML value.
    let rec mapTomlType (tob: Nett.TomlObject) =
        match tob with
        | :? Nett.TomlBool        -> TomlValueType(TomlBool)
        | :? Nett.TomlInt         -> TomlValueType(TomlInt)
        | :? Nett.TomlFloat       -> TomlValueType(TomlFloat)
        | :? Nett.TomlString      -> TomlValueType(TomlString)
        | :? Nett.TomlDateTime    -> TomlValueType(TomlDate)
        | :? Nett.TomlTimeSpan    -> TomlValueType(TomlTimespan)
        | :? Nett.TomlArray as t when t.Length = 0 -> TomlArray(TomlObject)
        | :? Nett.TomlArray as t  -> TomlArray(mapTomlType t.[0])
        | :? Nett.TomlTable as t  ->
            t.Rows
            |> Seq.map (fun v -> v.Key, mapTomlType v.Value)
            |> Map.ofSeq
            |> TomlTable
        | :? Nett.TomlTableArray as ta when ta.Count = 0 -> TomlTableArray(TomlTable(Map.empty))
        | :? Nett.TomlTableArray as ta                   -> TomlTableArray(mapTomlType ta.[0])
        | _ -> failwith "unsupported TOML type"

    /// Adds and returns a new provided type-definition to the type-container, or returns the existing definition.
    let private addProvidedType (typeContainer: ProvidedTypeDefinition) (knownTypes: Dictionary<_,_>) (tomlType: TomlType) (providedType: #Type) =
        let found, itemExisting = knownTypes.TryGetValue(tomlType)
        if found
        then itemExisting
        else typeContainer.AddMember providedType
             knownTypes.Add(tomlType, providedType)
             providedType

    type ProvidedTypeKind =
        | RootType of string * Assembly * string
        | NestedType of ProvidedTypeDefinition * Dictionary<TomlType,Type>

    let private extractTomlValue tomlType providedType (expr: Nett.TomlObject Expr) =
        let extractor =
            match tomlType with
            | TomlValueType(TomlBool)     -> <@@ ((%expr) :?> Nett.TomlBool).Value @@>
            | TomlValueType(TomlInt)      -> <@@ ((%expr) :?> Nett.TomlInt).Value @@>
            | TomlValueType(TomlFloat)    -> <@@ ((%expr) :?> Nett.TomlFloat).Value @@>
            | TomlValueType(TomlString)   -> <@@ ((%expr) :?> Nett.TomlString).Value @@>
            | TomlValueType(TomlDate)     -> <@@ ((%expr) :?> Nett.TomlDateTime).Value @@>
            | TomlValueType(TomlTimespan) -> <@@ ((%expr) :?> Nett.TomlTimeSpan).Value @@>
            | _ -> expr :> Expr
        Expr.Coerce (extractor, providedType)

    /// Translate a TOML structure description to a provided TOML type.
    let rec provideTypedToml (typeKind: ProvidedTypeKind) tomlType =
        match tomlType with
        | TomlValueType(TomlBool)     -> typeof<bool>
        | TomlValueType(TomlInt)      -> typeof<int64>
        | TomlValueType(TomlFloat)    -> typeof<double>
        | TomlValueType(TomlString)   -> typeof<string>
        | TomlValueType(TomlDate)     -> typeof<DateTime> // Nett should use DateTimeOffset
        | TomlValueType(TomlTimespan) -> typeof<TimeSpan>
        | TomlArray(TomlObject)       -> typeof<Nett.TomlArray>
        | TomlTable(x)                 when x = Map.empty -> typeof<Nett.TomlTable>
        | TomlTableArray(TomlTable(x)) when x = Map.empty -> typeof<Nett.TomlTableArray>
        | TomlArray(x) ->
            let (NestedType (typeContainer, knownTypes)) = typeKind
            let taTy = ProvidedTypeDefinition(
                            "TomlArray,"+(x.ToString())
                            , Some typeof<Nett.TomlArray>)

            let itemTy = provideTypedToml typeKind x
                         |> addProvidedType typeContainer knownTypes x

            let accessorM = ProvidedMethod(
                                "Item"
                                , [ProvidedParameter("index", typeof<int>, false, false)]
                                , itemTy
                                , InvokeCode= fun [this ; index] ->
                                    <@ (%%this:Nett.TomlArray).[%%index] @>
                                    |> extractTomlValue x itemTy)
            taTy.AddMember accessorM

            upcast taTy
        | TomlTableArray(x) ->
            let (NestedType (typeContainer, knownTypes)) = typeKind
            let taTy = ProvidedTypeDefinition(
                            "TomlTableArray,"+(x.ToString())
                            , Some typeof<Nett.TomlTableArray>)

            let itemTy = provideTypedToml typeKind x
                         |> addProvidedType typeContainer knownTypes x

            let accessorM = ProvidedMethod(
                                "Item"
                                , [ProvidedParameter("index", typeof<int>, false, false)]
                                , itemTy
                                , InvokeCode= fun [this ; index] ->
                                    <@ (%%this:Nett.TomlTableArray).[%%index] :> Nett.TomlObject @>
                                    |> extractTomlValue x itemTy)
            taTy.AddMember accessorM

            upcast taTy
        | TomlTable(x) as t ->
            let ttTy, typeContainer, knownTypes =
                match typeKind with
                | RootType (typename, assembly, rootNamespace) ->
                    let ty = ProvidedTypeDefinition(assembly, rootNamespace, typename, Some typeof<Nett.TomlTable>)
                    ty, ty, Dict.empty ()
                | NestedType (typeContainer, knownTypes) ->
                     let ty = ProvidedTypeDefinition(t.ToString(), Some typeof<Nett.TomlTable>)
                     ty, typeContainer, knownTypes

            let textP = ProvidedParameter("text", typeof<string>)
            let ctorM = ProvidedConstructor([textP], InvokeCode= fun [text] -> <@@ Nett.Toml.ReadString %%text @@>)
            ttTy.AddMember ctorM

            for name, ty in x |> Map.toSeq do
                let pretTy = provideTypedToml (NestedType (typeContainer, knownTypes)) ty
                             |> addProvidedType typeContainer knownTypes ty

                let pTy = ProvidedProperty(name, pretTy, GetterCode= fun [this] ->
                                let key = Expr.Value name
                                <@ (%%this:Nett.TomlTable).[%%key] @>
                                |> extractTomlValue ty pretTy)
                ttTy.AddMember pTy
            upcast ttTy
        | x                 -> failwith (sprintf "unsupported TomlType %O" x)

[<TypeProvider>]
type TomlProvider(config: TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces()

    let thisAssembly = Assembly.GetExecutingAssembly()
    let rootNamespace = "Gimpf.Toml"
    let tomlDocTy = ProvidedTypeDefinition(thisAssembly, rootNamespace, "TomlDoc", Some typeof<Nett.TomlTable>)

    do tomlDocTy.DefineStaticParameters(
        [ProvidedStaticParameter("sample", typeof<string>)],
        fun typeName parameterValues ->
            match parameterValues with
            | [| :? string as sample |] ->
                let sampleTable = Nett.Toml.ReadString sample
                let rootTableTy = TypeMapper.mapTomlType sampleTable
                downcast (TypeMapper.provideTypedToml (TypeMapper.RootType (typeName, thisAssembly, rootNamespace)) rootTableTy)
            | _ -> failwith "invalid arguments")

    do this.AddNamespace(rootNamespace, [tomlDocTy])

[<assembly:TypeProviderAssembly>]
do()
