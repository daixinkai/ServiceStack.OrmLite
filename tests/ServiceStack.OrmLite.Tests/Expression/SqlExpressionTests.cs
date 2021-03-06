﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceStack.DataAnnotations;
using ServiceStack.Html;
using ServiceStack.OrmLite.Tests.UseCase;
using ServiceStack.Text;

namespace ServiceStack.OrmLite.Tests.Expression
{
    public class LetterFrequency
    {
        [AutoIncrement]
        public int Id { get; set; }

        public string Letter { get; set; }
    }

    public class LetterWeighting
    {
        public long LetterFrequencyId { get; set; }
        public int Weighting { get; set; }
    }

    public class LetterStat
    {
        [AutoIncrement]
        public int Id { get; set; }
        public long LetterFrequencyId { get; set; }
        public string Letter { get; set; }
        public int Weighting { get; set; }
    }

    public class SqlExpressionTests : ExpressionsTestBase
    {
        private int letterFrequenceMaxId;
        private int letterFrequencyMinId;
        private int letterFrequencySumId;

        private void GetIdStats(IDbConnection db)
        {
            letterFrequenceMaxId = db.Scalar<int>(db.From<LetterFrequency>().Select(Sql.Max("Id")));
            letterFrequencyMinId = db.Scalar<int>(db.From<LetterFrequency>().Select(Sql.Min("Id")));
            letterFrequencySumId = db.Scalar<int>(db.From<LetterFrequency>().Select(Sql.Sum("Id")));
        }

        public static void InitLetters(IDbConnection db)
        {
            db.DropAndCreateTable<LetterFrequency>();

            db.Insert(new LetterFrequency { Letter = "A" });
            db.Insert(new LetterFrequency { Letter = "B" });
            db.Insert(new LetterFrequency { Letter = "B" });
            db.Insert(new LetterFrequency { Letter = "C" });
            db.Insert(new LetterFrequency { Letter = "C" });
            db.Insert(new LetterFrequency { Letter = "C" });
            db.Insert(new LetterFrequency { Letter = "D" });
            db.Insert(new LetterFrequency { Letter = "D" });
            db.Insert(new LetterFrequency { Letter = "D" });
            db.Insert(new LetterFrequency { Letter = "D" });
        }

        [Test]
        public void Can_select_Dictionary_with_SqlExpression()
        {
            using (var db = OpenDbConnection())
            {
                InitLetters(db);

                var query = db.From<LetterFrequency>()
                  .Select(x => new { x.Letter, count = Sql.Count("*") })
                  .Where(q => q.Letter != "D")
                  .GroupBy(x => x.Letter);

                query.ToSelectStatement().Print();

                var map = new SortedDictionary<string, int>(db.Dictionary<string, int>(query));
                Assert.That(map.EquivalentTo(new Dictionary<string, int> {
                    { "A", 1 }, { "B", 2 }, { "C", 3 },
                }));
            }
        }

        [Test]
        public void Can_select_ColumnDistinct_with_SqlExpression()
        {
            using (var db = OpenDbConnection())
            {
                InitLetters(db);

                var query = db.From<LetterFrequency>()
                  .Where(q => q.Letter != "D")
                  .Select(x => x.Letter);

                query.ToSelectStatement().Print();

                var uniqueLetters = db.ColumnDistinct<string>(query);
                Assert.That(uniqueLetters.EquivalentTo(new[] { "A", "B", "C" }));
            }
        }

        [Test]
        public void Can_Select_as_List_Object()
        {
            using (var db = OpenDbConnection())
            {
                InitLetters(db);
                GetIdStats(db);

                var query = db.From<LetterFrequency>()
                  .Select("COUNT(*), MAX(Id), MIN(Id), Sum(Id)");

                query.ToSelectStatement().Print();

                var results = db.Select<List<object>>(query);

                Assert.That(results.Count, Is.EqualTo(1));

                var result = results[0];
                CheckDbTypeInsensitiveEquivalency(result);

                var single = db.Single<List<object>>(query);
                CheckDbTypeInsensitiveEquivalency(single);

                result.PrintDump();
            }
        }

        private void CheckDbTypeInsensitiveEquivalency(List<object> result)
        {
            Assert.That(Convert.ToInt64(result[0]), Is.EqualTo(10));
            Assert.That(Convert.ToInt64(result[1]), Is.EqualTo(letterFrequenceMaxId));
            Assert.That(Convert.ToInt64(result[2]), Is.EqualTo(letterFrequencyMinId));
            Assert.That(Convert.ToInt64(result[3]), Is.EqualTo(letterFrequencySumId));
        }

        [Test]
        public void Can_Select_as_Dictionary_Object()
        {
            using (var db = OpenDbConnection())
            {
                InitLetters(db);
                GetIdStats(db);

                var query = db.From<LetterFrequency>()
                  .Select("COUNT(*) \"Count\", MAX(Id) \"Max\", MIN(Id) \"Min\", Sum(Id) \"Sum\"");

                query.ToSelectStatement().Print();

                var results = db.Select<Dictionary<string,object>>(query);

                Assert.That(results.Count, Is.EqualTo(1));

                var result = results[0];
                CheckDbTypeInsensitiveEquivalency(result);

                var single = db.Single<Dictionary<string, object>>(query);
                CheckDbTypeInsensitiveEquivalency(single);

                results.PrintDump();
            }
        }

        private void CheckDbTypeInsensitiveEquivalency(Dictionary<string, object> result)
        {
            Assert.That(Convert.ToInt64(result["Count"]), Is.EqualTo(10));
            Assert.That(Convert.ToInt64(result["Max"]), Is.EqualTo(letterFrequenceMaxId));
            Assert.That(Convert.ToInt64(result["Min"]), Is.EqualTo(letterFrequencyMinId));
            Assert.That(Convert.ToInt64(result["Sum"]), Is.EqualTo(letterFrequencySumId));
        }

        [Test]
        public void Can_select_Object()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();
                var id = db.Insert(new LetterFrequency {Id = 1, Letter = "A"}, selectIdentity: true);

                var result = db.Scalar<object>(db.From<LetterFrequency>().Select(x => x.Letter));
                Assert.That(result, Is.EqualTo("A"));

                result = db.Scalar<object>(db.From<LetterFrequency>().Select(x => x.Id));
                Assert.That(Convert.ToInt64(result), Is.EqualTo(id));
            }
        }

        [Test]
        public void Can_select_limit_with_SqlExpression()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();
                db.DropAndCreateTable<LetterWeighting>();

                var letters = "A,B,C,D,E".Split(',');
                var i = 0;
                letters.Each(letter =>
                {
                    var id = db.Insert(new LetterFrequency { Letter = letter }, selectIdentity: true);
                    db.Insert(new LetterWeighting { LetterFrequencyId = id, Weighting = ++i * 10 });
                });

                var results = db.Select(db.From<LetterFrequency>().Limit(3));
                Assert.That(results.Count, Is.EqualTo(3));

                results = db.Select(db.From<LetterFrequency>().Skip(3));
                Assert.That(results.Count, Is.EqualTo(2));

                results = db.Select(db.From<LetterFrequency>().Limit(1, 2));
                Assert.That(results.Count, Is.EqualTo(2));
                Assert.That(results.ConvertAll(x => x.Letter), Is.EquivalentTo(new[] { "B", "C" }));

                results = db.Select(db.From<LetterFrequency>().Skip(1).Take(2));
                Assert.That(results.ConvertAll(x => x.Letter), Is.EquivalentTo(new[] { "B", "C" }));

                results = db.Select(db.From<LetterFrequency>()
                    .OrderByDescending(x => x.Letter)
                    .Skip(1).Take(2));
                Assert.That(results.ConvertAll(x => x.Letter), Is.EquivalentTo(new[] { "D", "C" }));
            }
        }

        [Test]
        public void Can_select_limit_with_JoinSqlBuilder()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();
                db.DropAndCreateTable<LetterWeighting>();

                var letters = "A,B,C,D,E".Split(',');
                var i = 0;
                letters.Each(letter =>
                {
                    var id = db.Insert(new LetterFrequency { Letter = letter }, selectIdentity: true);
                    db.Insert(new LetterWeighting { LetterFrequencyId = id, Weighting = ++i * 10 });
                });

                var joinFn = new Func<JoinSqlBuilder<LetterFrequency, LetterWeighting>>(() =>
                    new JoinSqlBuilder<LetterFrequency, LetterWeighting>()
                        .Join<LetterFrequency, LetterWeighting>(x => x.Id, x => x.LetterFrequencyId)
                    );

                var results = db.Select<LetterFrequency>(joinFn());
                Assert.That(results.Count, Is.EqualTo(5));

                results = db.Select<LetterFrequency>(joinFn().Limit(3));
                Assert.That(results.Count, Is.EqualTo(3));

                results = db.Select<LetterFrequency>(joinFn().Skip(3));
                Assert.That(results.Count, Is.EqualTo(2));

                results = db.Select<LetterFrequency>(joinFn().Limit(1, 2));
                Assert.That(results.Count, Is.EqualTo(2));
                Assert.That(results.ConvertAll(x => x.Letter), Is.EquivalentTo(new[] { "B", "C" }));

                results = db.Select<LetterFrequency>(joinFn().Skip(1).Take(2));
                Assert.That(results.ConvertAll(x => x.Letter), Is.EquivalentTo(new[] { "B", "C" }));

                results = db.Select<LetterFrequency>(joinFn()
                    .OrderByDescending<LetterFrequency>(x => x.Letter)
                    .Skip(1).Take(2));
                Assert.That(results.ConvertAll(x => x.Letter), Is.EquivalentTo(new[] { "D", "C" }));
            }
        }

        [Test]
        public void Can_add_basic_joins_with_SqlExpression()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();
                db.DropAndCreateTable<LetterStat>();

                var letters = "A,B,C,D,E".Split(',');
                var i = 0;
                letters.Each(letter =>
                {
                    var id = db.Insert(new LetterFrequency { Letter = letter }, selectIdentity: true);
                    db.Insert(new LetterStat
                    {
                        LetterFrequencyId = id,
                        Letter = letter,
                        Weighting = ++i * 10
                    });
                });

                db.Insert(new LetterFrequency { Letter = "F" });

                Assert.That(db.Count<LetterFrequency>(), Is.EqualTo(6));

                var results = db.Select(db.From<LetterFrequency, LetterStat>());
                db.GetLastSql().Print();
                Assert.That(results.Count, Is.EqualTo(5));

                results = db.Select(db.From<LetterFrequency, LetterStat>((x, y) => x.Id == y.LetterFrequencyId));
                db.GetLastSql().Print();
                Assert.That(results.Count, Is.EqualTo(5));

                results = db.Select(db.From<LetterFrequency>()
                    .Join<LetterFrequency, LetterStat>((x, y) => x.Id == y.LetterFrequencyId));
                db.GetLastSql().Print();
                Assert.That(results.Count, Is.EqualTo(5));
            }
        }

        [Test]
        public void Can_do_ToCountStatement_with_SqlExpression_if_where_expression_refers_to_joined_table()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();
                db.DropAndCreateTable<LetterStat>();

                var letterFrequency = new LetterFrequency { Letter = "A" };
                letterFrequency.Id = (int)db.Insert(letterFrequency, true);

                db.Insert(new LetterStat { Letter = "A", LetterFrequencyId = letterFrequency.Id, Weighting = 1 });

                var expr = db.From<LetterFrequency>()
                    .Join<LetterFrequency, LetterStat>()
                    .Where<LetterStat>(x => x.Id > 0);

                var count = db.SqlScalar<long>(expr.ToCountStatement(), expr.Params.ToDictionary(param => param.ParameterName, param => param.Value));

                Assert.That(count, Is.GreaterThan(0));

                count = db.Count(db.From<LetterFrequency>().Join<LetterStat>().Where<LetterStat>(x => x.Id > 0));

                Assert.That(count, Is.GreaterThan(0));

                Assert.That(
                    db.Exists(db.From<LetterFrequency>().Join<LetterStat>().Where<LetterStat>(x => x.Id > 0)));
            }
        }

        [Test]
        public void Can_do_ToCountStatement_with_SqlExpression_if_expression_has_groupby()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();

                db.Insert(new LetterFrequency { Letter = "A" });
                db.Insert(new LetterFrequency { Letter = "A" });
                db.Insert(new LetterFrequency { Letter = "A" });
                db.Insert(new LetterFrequency { Letter = "B" });
                db.Insert(new LetterFrequency { Letter = "B" });
                db.Insert(new LetterFrequency { Letter = "B" });
                db.Insert(new LetterFrequency { Letter = "B" });

                var query = db.From<LetterFrequency>()
                    .Select(x => x.Letter)
                    .GroupBy(x => x.Letter);

                var count = db.Count(query);
                db.GetLastSql().Print();
                Assert.That(count, Is.EqualTo(7)); //Sum of Counts returned [3,4]

                var rowCount = db.RowCount(query);
                db.GetLastSql().Print();
                Assert.That(rowCount, Is.EqualTo(2));

                rowCount = db.Select(query).Count;
                db.GetLastSql().Print();
                Assert.That(rowCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void Can_select_RowCount_with_db_params()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();

                db.Insert(new LetterFrequency { Letter = "A" });
                db.Insert(new LetterFrequency { Letter = "A" });
                db.Insert(new LetterFrequency { Letter = "A" });
                db.Insert(new LetterFrequency { Letter = "B" });
                db.Insert(new LetterFrequency { Letter = "B" });
                db.Insert(new LetterFrequency { Letter = "B" });
                db.Insert(new LetterFrequency { Letter = "B" });

                var query = db.From<LetterFrequency>()
                    .Where(x => x.Letter == "B")
                    .Select(x => x.Letter);

                var rowCount = db.RowCount(query);
                db.GetLastSql().Print();
                Assert.That(rowCount, Is.EqualTo(4));

                var table = typeof(LetterFrequency).Name.SqlTable();

                rowCount = db.RowCount("SELECT * FROM {0} WHERE Letter = @p1".Fmt(table), new { p1 = "B" });
                Assert.That(rowCount, Is.EqualTo(4));

                rowCount = db.RowCount("SELECT * FROM {0} WHERE Letter = @p1".Fmt(table), 
                    new[] { db.CreateParam("p1", "B") });
                Assert.That(rowCount, Is.EqualTo(4));
            }
        }

        [Test]
        public void Can_get_RowCount_if_expression_has_OrderBy()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();

                db.Insert(new LetterFrequency { Letter = "A" });
                db.Insert(new LetterFrequency { Letter = "B" });
                db.Insert(new LetterFrequency { Letter = "B" });

                var query = db.From<LetterFrequency>()
                    .Select(x => x.Letter)
                    .OrderBy(x => x.Id);

                var rowCount = db.RowCount(query);
                Assert.That(rowCount, Is.EqualTo(3));

                rowCount = db.Select(query).Count;
                Assert.That(rowCount, Is.EqualTo(3));
            }
        }

        [Test]
        public void Can_OrderBy_Fields_with_different_sort_directions()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<LetterFrequency>();
                db.DropAndCreateTable<LetterStat>();

                var insertedIds = new List<long>();
                "A,B,B,C,C,C,D,D,E".Split(',').Each(letter => {
                    insertedIds.Add(db.Insert(new LetterFrequency { Letter = letter }, selectIdentity: true));
                });

                var rows = db.Select(db.From<LetterFrequency>().OrderByFields("Letter", "Id"));
                Assert.That(rows.Map(x => x.Letter), Is.EquivalentTo("A,B,B,C,C,C,D,D,E".Split(',')));
                Assert.That(rows.Map(x => x.Id), Is.EquivalentTo(insertedIds));

                rows = db.Select(db.From<LetterFrequency>().OrderByFields("Letter", "-Id"));
                Assert.That(rows.Map(x => x.Letter), Is.EquivalentTo("A,B,B,C,C,C,D,D,E".Split(',')));
                Assert.That(rows.Map(x => x.Id), Is.EquivalentTo(insertedIds));

                rows = db.Select(db.From<LetterFrequency>().OrderByFieldsDescending("Letter", "-Id"));
                Assert.That(rows.Map(x => x.Letter), Is.EquivalentTo("E,D,D,C,C,C,B,B,A".Split(',')));
                Assert.That(rows.Map(x => x.Id), Is.EquivalentTo(Enumerable.Reverse(insertedIds)));
            }
        }

        [Test]
        public void Can_select_limit_on_Table_with_References()
        {
            //This version of MariaDB doesn't yet support 'LIMIT & IN/ALL/ANY/SOME subquery'
            if (Dialect == Dialect.MySql) return;

            //Only one expression can be specified in the select list when the subquery is not introduced with EXISTS.
            if (Dialect == Dialect.SqlServer) return;

            using (var db = OpenDbConnection())
            {
                CustomerOrdersUseCase.DropTables(db); //Has conflicting 'Order' table
                db.DropAndCreateTable<Order>();
                db.DropAndCreateTable<Customer>();
                db.DropAndCreateTable<CustomerAddress>();

                var customer1 = LoadReferencesTests.GetCustomerWithOrders("1");
                db.Save(customer1, references: true);

                var customer2 = LoadReferencesTests.GetCustomerWithOrders("2");
                db.Save(customer2, references: true);

                var results = db.LoadSelect(db.From<Customer>()
                    .OrderBy(x => x.Id)
                    .Limit(1, 1));

                //db.GetLastSql().Print();

                Assert.That(results.Count, Is.EqualTo(1));
                Assert.That(results[0].Name, Is.EqualTo("Customer 2"));
                Assert.That(results[0].PrimaryAddress.AddressLine1, Is.EqualTo("2 Humpty Street"));
                Assert.That(results[0].Orders.Count, Is.EqualTo(2));

                results = db.LoadSelect(db.From<Customer>()
                    .Join<CustomerAddress>()
                    .OrderBy(x => x.Id)
                    .Limit(1, 1));

                db.GetLastSql().Print();

                Assert.That(results.Count, Is.EqualTo(1));
                Assert.That(results[0].Name, Is.EqualTo("Customer 2"));
                Assert.That(results[0].PrimaryAddress.AddressLine1, Is.EqualTo("2 Humpty Street"));
                Assert.That(results[0].Orders.Count, Is.EqualTo(2));
            }
        }

        public class TableA
        {
            public int Id { get; set; }
            public bool Bool { get; set; }
            public string Name { get; set; }
        }

        public class TableB
        {
            public int Id { get; set; }
            public int TableAId { get; set; }
            public string Name { get; set; }
        }

        [Test]
        public void Can_query_bools()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<TableA>();
                db.DropAndCreateTable<TableB>();

                db.Insert(new TableA { Id = 1, Bool = false });
                db.Insert(new TableA { Id = 2, Bool = true });
                db.Insert(new TableB { Id = 1, TableAId = 1 });
                db.Insert(new TableB { Id = 2, TableAId = 2 });

                var q = db.From<TableA>()
                    .LeftJoin<TableB>((a, b) => a.Id == b.Id)
                    .Where(a => !a.Bool);

                var result = db.Single(q);
                var lastSql = db.GetLastSql();
                lastSql.Print();
                Assert.That(result.Id, Is.EqualTo(1));
                Assert.That(lastSql, Is.Not.StringContaining("NOT"));

                q = db.From<TableA>()
                    .Where(a => !a.Bool)
                    .LeftJoin<TableB>((a, b) => a.Id == b.Id);

                result = db.Single(q);
                lastSql = db.GetLastSql();
                lastSql.Print();
                Assert.That(result.Id, Is.EqualTo(1));
                Assert.That(lastSql, Is.Not.StringContaining("NOT"));


                q = db.From<TableA>()
                    .Where(a => !a.Bool);

                result = db.Single(q);
                lastSql = db.GetLastSql();
                lastSql.Print();
                Assert.That(result.Id, Is.EqualTo(1));
                Assert.That(lastSql, Is.Not.StringContaining("NOT"));

                q = db.From<TableA>()
                    .LeftJoin<TableB>((a, b) => a.Id == b.Id)
                    .Where(a => a.Bool);

                result = db.Single(q);
                db.GetLastSql().Print();
                Assert.That(result.Id, Is.EqualTo(2));

                q = db.From<TableA>()
                    .Where(a => a.Bool)
                    .LeftJoin<TableB>((a, b) => a.Id == b.Id);

                result = db.Single(q);
                db.GetLastSql().Print();
                Assert.That(result.Id, Is.EqualTo(2));


                q = db.From<TableA>()
                    .Where(a => a.Bool);

                result = db.Single(q);
                db.GetLastSql().Print();
                Assert.That(result.Id, Is.EqualTo(2));
            }
        }

        [Test]
        public void Can_order_by_Joined_table()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<TableA>();
                db.DropAndCreateTable<TableB>();

                db.Insert(new TableA { Id = 1, Bool = false });
                db.Insert(new TableA { Id = 2, Bool = true });
                db.Insert(new TableB { Id = 1, TableAId = 1, Name = "Z" });
                db.Insert(new TableB { Id = 2, TableAId = 2, Name = "A" });

                var q = db.From<TableA>()
                    .Join<TableB>()
                    .OrderBy(x => x.Id);

                var rows = db.Select(q);
                db.GetLastSql().Print();
                Assert.That(rows.Map(x => x.Id), Is.EqualTo(new[] { 1, 2 }));


                q = db.From<TableA>()
                    .Join<TableB>()
                    .OrderBy<TableB>(x => x.Name);

                rows = db.Select(q);
                db.GetLastSql().Print();
                Assert.That(rows.Map(x => x.Id), Is.EqualTo(new[] { 2, 1 }));
            }
        }

        [Test]
        public void Can_find_missing_rows_from_Left_Join_on_int_primary_key()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<TableA>();
                db.DropAndCreateTable<TableB>();

                db.Insert(new TableA { Id = 1, Bool = true, Name = "A" });
                db.Insert(new TableA { Id = 2, Bool = true, Name = "B" });
                db.Insert(new TableA { Id = 3, Bool = true, Name = "C" });
                db.Insert(new TableB { Id = 1, TableAId = 1, Name = "Z" });

                var missingNames = db.Column<string>(
                    db.From<TableA>()
                      .LeftJoin<TableB>((a, b) => a.Id == b.Id)
                      .Where<TableB>(b => b.Id == null)
                      .Select(a => a.Name));

                Assert.That(missingNames, Is.EquivalentTo(new[] { "B", "C" }));
            }
        }

        public class CrossJoinTableA
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class CrossJoinTableB
        {
            public int Id { get; set; }
            public int Value { get; set; }
        }

        public class CrossJoinResult
        {
            public int CrossJoinTableAId { get; set; }
            public string Name { get; set; }
            public int CrossJoinTableBId { get; set; }
            public int Value { get; set; }

            public override bool Equals(object obj)
            {
                var other = obj as CrossJoinResult;
                if (other == null)
                    return false;

                return CrossJoinTableAId == other.CrossJoinTableAId && string.Equals(Name, other.Name) && CrossJoinTableBId == other.CrossJoinTableBId && Value == other.Value;
            }
        }

        [Test]
        public void Can_perform_a_crossjoin_without_a_join_expression()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<CrossJoinTableA>();
                db.DropAndCreateTable<CrossJoinTableB>();

                db.Insert(new CrossJoinTableA { Id = 1, Name = "Foo" });
                db.Insert(new CrossJoinTableA { Id = 2, Name = "Bar" });
                db.Insert(new CrossJoinTableB { Id = 5, Value = 3 });
                db.Insert(new CrossJoinTableB { Id = 6, Value = 42 });

                var q = db.From<CrossJoinTableA>()
                          .CrossJoin<CrossJoinTableB>()
                          .OrderBy<CrossJoinTableA>(x => x.Id)
                          .ThenBy<CrossJoinTableB>(x => x.Id);
                var result = db.Select<CrossJoinResult>(q);

                db.GetLastSql().Print();

                Assert.That(result.Count, Is.EqualTo(4));
                var expected = new List<CrossJoinResult> 
                {
                    new CrossJoinResult { CrossJoinTableAId = 1, Name = "Foo", CrossJoinTableBId = 5, Value = 3 },
                    new CrossJoinResult { CrossJoinTableAId = 1, Name = "Foo", CrossJoinTableBId = 6, Value = 42 },
                    new CrossJoinResult { CrossJoinTableAId = 2, Name = "Bar", CrossJoinTableBId = 5, Value = 3},
                    new CrossJoinResult { CrossJoinTableAId = 2, Name = "Bar", CrossJoinTableBId = 6, Value = 42},
                };
                Assert.That(result, Is.EquivalentTo(expected));
            }
        }

        [Test]
        public void Can_perform_a_crossjoin_with_a_join_expression()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<CrossJoinTableA>();
                db.DropAndCreateTable<CrossJoinTableB>();

                db.Insert(new CrossJoinTableA { Id = 1, Name = "Foo" });
                db.Insert(new CrossJoinTableA { Id = 2, Name = "Bar" });
                db.Insert(new CrossJoinTableB { Id = 5, Value = 3 });
                db.Insert(new CrossJoinTableB { Id = 6, Value = 42 });
                db.Insert(new CrossJoinTableB { Id = 7, Value = 56 });

                var q = db.From<CrossJoinTableA>().CrossJoin<CrossJoinTableB>((a, b) => b.Id > 5 && a.Id < 2).OrderBy<CrossJoinTableA>(x => x.Id).ThenBy<CrossJoinTableB>(x => x.Id);
                var result = db.Select<CrossJoinResult>(q);

                db.GetLastSql().Print();

                Assert.That(result.Count, Is.EqualTo(2));
                var expected = new List<CrossJoinResult> 
                {
                    new CrossJoinResult { CrossJoinTableAId = 1, Name = "Foo", CrossJoinTableBId = 6, Value = 42 },
                    new CrossJoinResult { CrossJoinTableAId = 1, Name = "Foo", CrossJoinTableBId = 7, Value = 56 },
                };
                Assert.That(result, Is.EquivalentTo(expected));
            }
        }

        class JoinTest
        {
            public int Id { get; set; }
        }

        class JoinTestChild
        {
            public int Id { get; set; }

            public int ParentId { get; set; }

            public bool IsActive { get; set; }
        }

        [Test]
        public void Issue_Bool_JoinTable_Expression()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<JoinTest>();
                db.DropAndCreateTable<JoinTestChild>();

                db.InsertAll(new[] {
                    new JoinTest { Id = 1, },
                    new JoinTest { Id = 2, }
                });

                db.InsertAll(new[] {
                    new JoinTestChild
                    {
                        Id = 1,
                        ParentId = 1,
                        IsActive = true
                    },
                    new JoinTestChild
                    {
                        Id = 2,
                        ParentId = 2,
                        IsActive = false
                    }
                });

                var q = db.From<JoinTestChild>();
                q.Where(x => !x.IsActive);
                Assert.That(db.Select(q).Count, Is.EqualTo(1));

                var qSub = db.From<JoinTest>();
                qSub.Join<JoinTestChild>((x, y) => x.Id == y.ParentId);
                qSub.Where<JoinTestChild>(x => !x.IsActive); // This line is a bug!
                Assert.That(db.Select(qSub).Count, Is.EqualTo(1));
            }
        }

        public class Invoice
        {
            public int Id { get; set; }

            public int WorkflowId { get; set; }

            public int DocumentId { get; set; }

            public int PageCount { get; set; }

            public string DocumentStatus { get; set; }

            public string Extra { get; set; }
        }

        public class UsagePageInvoice
        {
            public int Id { get; set; }
            public int InvoiceId { get; set; }
        }

        [Test]
        public void Can_select_individual_columns()
        {
            using (var db = OpenDbConnection())
            {
                db.DropAndCreateTable<Invoice>();
                db.DropAndCreateTable<UsagePageInvoice>();

                db.Insert(new Invoice {
                    Id = 1, 
                    WorkflowId = 2, 
                    DocumentId = 3, 
                    PageCount = 4, 
                    DocumentStatus = "a",
                    Extra = "EXTRA"
                });

                var q = db.From<Invoice>()
                    .LeftJoin<Invoice, UsagePageInvoice>((i, upi) => i.Id == upi.InvoiceId)
                    .Where<Invoice>(i => (i.DocumentStatus == "a" || i.DocumentStatus == "b"))
                    .And<UsagePageInvoice>(upi => upi.Id == null)
                    .Select(c => new { c.Id, c.WorkflowId, c.DocumentId, c.DocumentStatus, c.PageCount });

                var result = db.Select(q).First();

                Assert.That(result.Id, Is.EqualTo(1));
                Assert.That(result.WorkflowId, Is.EqualTo(2));
                Assert.That(result.DocumentId, Is.EqualTo(3));
                Assert.That(result.PageCount, Is.EqualTo(4));
                Assert.That(result.DocumentStatus, Is.EqualTo("a"));
                Assert.That(result.Extra, Is.Null);
            }
        }
    }
}