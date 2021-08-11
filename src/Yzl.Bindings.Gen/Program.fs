open System
open System.Collections.Generic
open NJsonSchema

type Context =
  {
    EntryModuleName: string
    ParentKey: string
  }

type UrlOrFilePath =
 | Path of string
 | Url of string
with 
  static member ofString (value:string) =
    match value with
    | x when x.StartsWith("https://") || x.StartsWith("http://") -> Url x
    | _ -> Path value

let loadJson (url:UrlOrFilePath) =
  async {
    let! token = Async.CancellationToken
    
    match url with 
    | Url url -> 
      failwithf "not supported yet"
      return NJsonSchema.JsonSchema.CreateAnySchema()
    | Path file ->
      let! x =  (NJsonSchema.JsonSchema.FromFileAsync(file, token) |> Async.AwaitTask)
      return x
  }

[<EntryPoint>]
let main argv =
  let schemaUrl = UrlOrFilePath.ofString argv.[0]
  let schema = loadJson schemaUrl |> Async.RunSynchronously

  let capitalize (x:string) = Char.ToUpper(x.[0]).ToString() + x.[1..]
  let camelize (x:string) = Char.ToLower(x.[0]).ToString() + x.[1..]

  let matchNonEmpty =
   function
    | null -> None
    | defs ->
      match defs |> Seq.toList |> List.map (|KeyValue|) with
      | [] -> None
      | xs -> Some xs

  let matchType (typ:JsonObjectType) (x:JsonSchema) =
    match x with
    | x when x.Type = typ -> Some x
    | _ -> None 

  let (|Properties|_|) (s:JsonSchema) =
    s.Properties |> matchNonEmpty

  let (|PatternProperties|_|) (s:JsonSchema) =
    s.PatternProperties |> matchNonEmpty

  let (|Array2|_|) (s:JsonSchema) =
    match s.Items |> Seq.toList with
    | [] -> None
    | xs -> Some xs

  let (|OneOf|_|) (s:JsonSchema) =
    match s.OneOf |> Seq.toList with
    | [] -> None
    | xs -> Some xs

  let (|Array|_|) (s:JsonSchema) =
    match s.Item with
    | null -> None
    | v -> Some v

  let (|Enum|_|) (s:JsonSchema) =
    match s.Enumeration |> Seq.toList with
    | [] -> None
    | v -> Some (v, s)

  let (|String|_|) (s:JsonSchema) =
   s |> matchType JsonObjectType.String

  let (|Boolean|_|) (s:JsonSchema) =
   s |> matchType JsonObjectType.Boolean

  let (|Reference|_|) (s:JsonSchema) =
    match s with
    | x when x.Reference |> isNull |> not -> Some (x.Reference)
    | _ -> None

  let (|Definitions|_|) (s:JsonSchema) =
    s.Definitions |> matchNonEmpty

  let resolveDef ref = schema.Definitions |> Seq.find (fun (KeyValue(k,v)) -> v = ref)
  
  let enums = List<(string * string list)>()
  
  let nextIndex =
    let mutable z = 0
    fun () -> 
      z <- z+1
      z

  let rec render (s:JsonSchema) (ctx: Context) =
    match s with
    | Definitions (def) ->
      def |> Seq.iter (fun (k,v) ->
        let desc =
          match v.Description with
          | d when d |> String.IsNullOrWhiteSpace -> ""
          | d -> sprintf " - %s" d
        printfn "\n/// %s%s" k desc
        // if ctx.EntryModuleName = k 
        //   then printfn "[<AutoOpen>]"
        //   else ()
        printf "type %s() = " k
        render v { ctx with ParentKey = k }
        printfn ""
        printfn "  static member Default = %s()" k
        printfn "  static member yzl (build:(%s -> Yzl.NamedNode) list) : Yzl.Node = build |> List.map (fun f -> f %s.Default) |> Yzl.lift" k k
      )
    | Reference (ref) ->
      //let def = resolveDef ref
      
      printf "Yzl.map" //(camelize def.Key)
    | Array x ->
      //render x ctx
      printf "Yzl.seq"
    | OneOf xs ->
      //printf "/// oneof"
      let key = "union"
      let key = sprintf "%s%i" key (nextIndex ()) |> capitalize
      
      enums.Add((key, xs |> List.map (fun x -> 
        let def = resolveDef x.Reference
        def.Key
      )))
      printf "%s" key
    | Array2 xs ->
      printf "/// weird"
    | Enum (xs,v) ->
      let key =
        match v with
        | :? JsonSchemaProperty as p -> p.Name 
        | _ -> "AnonymousEnum"
      
      let key = sprintf "%s%i" key  (nextIndex ()) |> capitalize
      enums.Add((key, xs |> List.map string))
      printf "Yzl.str" //let's ignore type information for now
    | String x ->
      printf "Yzl.str"
    | Boolean x ->
      printf "Yzl.boolean"
    | PatternProperties xs ->
      xs |> Seq.iter (fun _ -> printf "Yzl.map")
    | Properties xs ->
      xs |> Seq.iter (fun (k,v) -> 
        let key = camelize k
        
        let rec annotation =
          function
          | String _ -> "^a"
          | Enum _ -> "string"
          | Boolean _ -> "bool"
          | Array a -> 
            let nested = annotation a
            sprintf "%s list" nested
          | PatternProperties _ -> "Yzl.NamedNode list"
          
          //| Reference ref -> 
            //let def = resolveDef ref 
            //sprintf "%s -> Yzl.NamedNode list" (def.Key)
            //sprintf ""
          | Reference ref ->
            let def = resolveDef ref
            sprintf "(%s -> Yzl.NamedNode) list" (def.Key)
          | _ -> "Yzl.Node" 

        let value =
          function
          | Reference ref -> 
             let def = resolveDef ref 
             sprintf"(value |> List.map (fun f -> f %s.Default))" (def.Key)
          | Array a ->
            match a with
            | Reference ref ->
              let def = resolveDef ref 
              sprintf"(value |> List.map (fun v -> v |> List.map (fun f -> f %s.Default)) |> Yzl.liftMany)" (def.Key)
            | _ -> "(value |> Yzl.liftMany)"

          |_ -> "value"

        printfn ""
        // match ctx.Type with
        // | RecordDefinition ->
        let escapedKey =
          match key with
          | "namespace" | "type" -> sprintf "``%s``" key
          | _ -> key
        
        match v.Description with
        | d when d |> String.IsNullOrWhiteSpace -> ()
        | d -> printfn "  /// %s" d
        printf """  static member inline %s (value: %s) (_:%s) = """ escapedKey (annotation v) ctx.ParentKey
        render v ctx
        printf """ "%s" %s""" key (value v)

        // | DefaultRecord -> printf "      %s = None" key
        // | Default -> ()
      )

    | x -> failwithf "No idea what to do! %A" x
  
  //TODO: obvs the namespace should be a parameter
  printfn "namespace rec Yzl.Bindings.Kustomize"
  printfn "open Yzl.Core"
  printfn ""

  // common - this must be generated too - this is a temp workaround
  printfn """type Common() =
  static member inline name (value: ^a) (_:'b) = Yzl.str "name" value
  static member inline kind (value: ^a) (_:'b) = Yzl.str "kind" value
  static member inline apiVersion (value: ^a) (_:'b) = Yzl.str "apiVersion" value
  static member inline _namespace (value: ^a) (_:'b) = Yzl.str "namespace" value
  """

  //TODO: the context is not used atm
  render schema {EntryModuleName = "Kustomization"; ParentKey = ""}

  //TODO: this is not needed atm
  // printfn ""
  // enums |> Seq.iter (fun (key, values) -> 
  //   printfn "type %s = %s" key (values |> Seq.map (capitalize >> (sprintf "| %s ")) |> String.concat "")
  // )


  0
