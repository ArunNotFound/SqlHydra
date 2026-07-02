using System;
using DuckDB.NET.Data;

class Program {
    static void Main() {
        try {
            var builder = new DuckDBConnectionStringBuilder("DataSource=:memory:;");
            Console.WriteLine("Parsed successfully.");
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.ToString());
        }
    }
}
