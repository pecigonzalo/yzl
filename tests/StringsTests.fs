namespace Yzl.Tests.Unit

module Strings =

  open Expecto
  open Yzl.Core
  open System.IO

  [<Tests>]
  let tests =
    let myText =  Yzl.str "myText"
    testList "generate" [
      
      test "Literal dash" {
        let expected = File.ReadAllText("./yaml/literal-dash.yaml")
        let yaml = ![
          Yzl.map "parent" [
            myText !|-
  // START: this space is left here purposely - do not remove
                                             """
                                             some
                                             - text should
                                             - have 
                                                 the indentation adjusted to the parent node
                                             """
  // END: this space is left here purposely - do not remove
          ]
        ]

        "Rendering failed" |> Expect.equal (yaml |> Yzl.render) expected
      }

      test "Literal" {
        let expected = File.ReadAllText("./yaml/literal.yaml")

        let yaml = ![
          Yzl.map "parent" [
           myText !|
  // START: this space is left here purposely - do not remove
               """
               some
               - text should : everything is allowed in here
               other-things 
                   - the indentation adjusted to the parent node
                   - 1
               """
  // END: this space is left here purposely - do not remove
          ]
        ]

        "Rendering failed" |> Expect.equal (yaml |> Yzl.render) expected
      }

      test "Folded dash" {
        let expected = File.ReadAllText("./yaml/folded-dash.yaml")
        let yaml = ![
          Yzl.map "parent" [
           myText !>-
  // START: this space is left here purposely - do not remove
                                             """
                                             rg             
                                               szf    
                                                 szgszdafsesf
                                               awe             
                                             ffe        
                                             """
  // END: this space is left here purposely - do not remove
          ]
        ]

        "Rendering failed" |> Expect.equal (yaml |> Yzl.render) expected
      }

      test "Folded" {
        let expected = File.ReadAllText("./yaml/folded.yaml")

        let yaml = ![
          Yzl.map "parent" [
           myText !>
  // START: this space is left here purposely - do not remove
                  """
                  lorem ipsum      
                  does not make that     
                      much of a sense i   
                                    ------- ----- 33nw    
                      e  
                  3r m4qlf853


                  
                  """
  // END: this space is left here purposely - do not remove
          ]
        ]

        "Rendering failed" |> Expect.equal (yaml |> Yzl.render) expected
      }

      test "Folded null" {
        let expected = File.ReadAllText("./yaml/folded-null.yaml")

        let yaml = ![
          Yzl.map "parent" [
           myText !> null
          ]
        ]

        "Rendering failed" |> Expect.equal (yaml |> Yzl.render) expected
      }

      
      

      //TODO: add null strings tests
    ]