module SqlHydra.Kafka.Tests.KafkaAstTests

open NUnit.Framework
open Swensen.Unquote
open SqlHydra.Kafka

[<Test>]
let ``Basic kstream builds select ksqlDB push query`` () =
    let query = 
        kstream {
            consume "UserClicks"
            select "UserId"
            select "Platform"
        }
    
    let expected = "SELECT UserId, Platform FROM UserClicks EMIT CHANGES;"
    test <@ query = expected @>

[<Test>]
let ``kstream with where builds conditional ksqlDB query`` () =
    let query =
        kstream {
            consume "UserClicks"
            where "Platform = 'Mobile'"
            where "Action = 'Buy'"
        }
    let expected = "SELECT * FROM UserClicks WHERE Platform = 'Mobile' AND Action = 'Buy' EMIT CHANGES;"
    test <@ query = expected @>

[<Test>]
let ``kstream with tumbling window and groupBy builds windowed ksqlDB query`` () =
    let query =
        kstream {
            consume "UserClicks"
            groupBy "UserId"
            window "TUMBLING (SIZE 5 MINUTES)"
        }
    let expected = "SELECT * FROM UserClicks WINDOW TUMBLING (SIZE 5 MINUTES) GROUP BY UserId EMIT CHANGES;"
    test <@ query = expected @>
