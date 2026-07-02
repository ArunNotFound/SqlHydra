namespace SqlHydra.Kafka

open SqlHydra.Domain

/// A mock Schema Provider that simulates polling Confluent Schema Registry
/// and generating F# records from Avro/Protobuf definitions.
module KafkaSchemaProvider =
    
    let getTopics () =
        [
            { 
                Name = "UserClicks"
                TypeMapping = { ClrType = "UserClick"; DbType = System.Data.DbType.Object; ProviderDbType = None; ColumnTypeAlias = "UserClick" }
                IsNullable = false
                IsPK = false
            }
        ]
