﻿using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace IntegrationTests.Infrastructure
{
    public class TestDoc<TKey> : IDocument<TKey>
                where TKey : IEquatable<TKey>
    {
        [BsonId]
        public TKey Id { get; set; }
        public int Version { get; set; }

        public TestDoc()
        {
            InitializeFields();
            Version = 2;
            Nested = new Nested
            {
                SomeDate = DateTime.UtcNow
            };
        }

        public string SomeContent { get; set; }

        public Nested Nested { get; set; }

        public TId Init<TId>()
        {
            var idTypeName = typeof(TKey).Name;
            switch (idTypeName)
            {
                case "Guid":
                    return (TId)(object)Guid.NewGuid();
                case "Int16":
                    return (TId)(object)GlobalVariables.Random.Next(1, short.MaxValue);
                case "Int32":
                    return (TId)(object)GlobalVariables.Random.Next(1, int.MaxValue);
                case "Int64":
                    return (TId)(object)(GlobalVariables.Random.NextLong(1, long.MaxValue));
                case "String":
                    return (TId)(object)Guid.NewGuid().ToString();
                default:
                    throw new NotSupportedException($"{idTypeName} is not supported.");
            }
        }

        private void InitializeFields()
        {
            Id = Init<TKey>();
        }
    }


    [TestFixture]
    public abstract class MongoDBTestBase<T, TKey> 
        where T: TestDoc<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        public T CreateTestDocument()
        {
            return new T();
        }

        public abstract string GetClassName();

        public List<T> CreateTestDocuments(int numberOfDocumentsToCreate)
        {
            var docs = new List<T>();
            for (var i = 0; i < numberOfDocumentsToCreate; i++)
            {
                docs.Add(new T());
            }
            return docs;
        }

        /// <summary>
        /// The partition key for the collection, if any
        /// </summary>
        protected string PartitionKey { get; set; }

        /// <summary>
        /// the name of the test class
        /// </summary>
        protected string TestClassName { get; set; }

        /// <summary>
        /// The name of the document used for tests
        /// </summary>
        protected string DocumentTypeName { get; set; }

        /// <summary>
        /// SUT: System Under Test
        /// </summary>
        protected static ITestRepository SUT { get; set; }

        public MongoDBTestBase()
        {
            var type = CreateTestDocument();
            DocumentTypeName = type.GetType().FullName;
            if (type is IPartitionedDocument)
            {
                PartitionKey = ((IPartitionedDocument)type).PartitionKey;
            }
            TestClassName = GetClassName();
        }

        [OneTimeSetUp]
        public void Init()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["MongoDbTests"].ConnectionString;
            SUT = new TestRepository(connectionString, "MongoDbTests");
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            // We drop the collection at the end of each test session.
            if (!string.IsNullOrEmpty(PartitionKey))
            {
                SUT.DropTestCollection<T>(PartitionKey);
            }
            else
            {
                SUT.DropTestCollection<T>();
            }
        }

        #region Add

        [Test]
        public void AddOne()
        {
            // Arrange
            var document = new T();
            // Act
            SUT.AddOne<T, TKey>(document);
            // Assert
            long count = string.IsNullOrEmpty(PartitionKey) ? SUT.Count<T, TKey>(e => e.Id.Equals(document.Id)) 
                                                            : SUT.Count<T, TKey>(e => e.Id.Equals(document.Id), PartitionKey);
            Assert.AreEqual(1, count, GetTestName());
        }

        [Test]
        public async Task AddOneAsync()
        {
            // Arrange
            var document = new T();
            // Act
            await SUT.AddOneAsync<T, TKey>(document);
            // Assert
            long count = string.IsNullOrEmpty(PartitionKey) ? SUT.Count<T, TKey>(e => e.Id.Equals(document.Id))
                                                            : SUT.Count<T, TKey>(e => e.Id.Equals(document.Id), PartitionKey);
            Assert.AreEqual(1, count, GetTestName());
        }

        [Test]
        public void AddMany()
        {
            // Arrange
            var documents = new List<T> { new T(), new T() };
            // Act
            SUT.AddMany<T, TKey>(documents);
            // Assert
            long count = string.IsNullOrEmpty(PartitionKey) ? SUT.Count<T, TKey>(e => e.Id.Equals(documents[0].Id)
                                                                                   || e.Id.Equals(documents[1].Id))
                                                            : SUT.Count<T, TKey>(e => e.Id.Equals(documents[0].Id)
                                                                                  || e.Id.Equals(documents[1].Id), PartitionKey);
            Assert.AreEqual(2, count, GetTestName());
        }

        [Test]
        public async Task AddManyAsync()
        {
            // Arrange
            var documents = new List<T> { new T(), new T() };
            // Act
            await SUT.AddManyAsync<T, TKey>(documents);
            // Assert
            long count = string.IsNullOrEmpty(PartitionKey) ? SUT.Count<T, TKey>(e => e.Id.Equals(documents[0].Id)
                                                                                   || e.Id.Equals(documents[1].Id))
                                                            : SUT.Count<T, TKey>(e => e.Id.Equals(documents[0].Id)
                                                                                  || e.Id.Equals(documents[1].Id), PartitionKey);
            Assert.AreEqual(2, count, GetTestName());
        }


        #endregion Add

        #region Read

        [Test]
        public async Task GetByIdAsync()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = await SUT.GetByIdAsync<T, TKey>(document.Id, PartitionKey);
            // Assert
            Assert.IsNotNull(result, GetTestName());
        }

        [Test]
        public void GetById()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = SUT.GetById<T, TKey>(document.Id, PartitionKey);
            // Assert
            Assert.IsNotNull(result, GetTestName());
        }


        [Test]
        public async Task PartitionedGetOneAsync()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = await SUT.GetOneAsync<T, TKey>(x => x.Id.Equals(document.Id), PartitionKey);
            // Assert
            Assert.IsNotNull(result, GetTestName());
        }

        [Test]
        public void GetOne()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = SUT.GetOne<T, TKey>(x => x.Id.Equals(document.Id), PartitionKey);
            // Assert
            Assert.IsNotNull(result, GetTestName());
        }

        [Test]
        public void GetCursor()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var cursor = SUT.GetCursor<T, TKey>(x => x.Id.Equals(document.Id), PartitionKey);
            var count = cursor.Count();
            // Assert
            Assert.AreEqual(1, count, GetTestName());
        }

        [Test]
        public async Task AnyAsyncReturnsTrue()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = await SUT.AnyAsync<T, TKey>(x => x.Id.Equals(document.Id), PartitionKey);
            // Assert
            Assert.AreEqual(true, result, GetTestName());
        }

        [Test]
        public async Task AnyAsyncReturnsFalse()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = await SUT.AnyAsync<T, TKey>(x => x.Id.Equals(document.Init<TKey>()), PartitionKey);
            // Assert
            Assert.AreEqual(false, result, GetTestName());
        }

        [Test]
        public void AnyReturnsTrue()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = SUT.Any<T, TKey>(x => x.Id.Equals(document.Id), PartitionKey);
            // Assert
            Assert.AreEqual(true, result, GetTestName());
        }

        [Test]
        public void AnyReturnsFalse()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = SUT.Any<T, TKey>(x => x.Id.Equals(document.Init<TKey>()), PartitionKey);
            // Assert
            Assert.AreEqual(false, result, GetTestName());
        }

        [Test]
        public async Task GetAllAsync()
        {
            // Arrange
            var documents = CreateTestDocuments(5);
            var content = GetContent();
            documents.ForEach(e => e.SomeContent = content);
            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = await SUT.GetAllAsync<T, TKey>(x => x.SomeContent == content, PartitionKey);
            // Assert
            Assert.AreEqual(5, result.Count, GetTestName());
        }

        [Test]
        public void GetAll()
        {
            // Arrange
            var documents = CreateTestDocuments(5);
            var content = GetContent();
            documents.ForEach(e => e.SomeContent = content);
            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = SUT.GetAll<T, TKey>(x => x.SomeContent == content, PartitionKey);
            // Assert
            Assert.AreEqual(5, result.Count, GetTestName());
        }

        [Test]
        public async Task CountAsync()
        {
            // Arrange
            var documents = CreateTestDocuments(5);
            var content = GetContent();
            documents.ForEach(e => e.SomeContent = content);
            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = await SUT.CountAsync<T, TKey>(x => x.SomeContent == content, PartitionKey);
            // Assert
            Assert.AreEqual(5, result, GetTestName());
        }

        [Test]
        public void Count()
        {
            // Arrange
            var documents = CreateTestDocuments(5);
            var content = GetContent();
            documents.ForEach(e => e.SomeContent = content);
            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = SUT.Count<T, TKey>(x => x.SomeContent == content, PartitionKey);
            // Assert
            Assert.AreEqual(5, result);
        }

        #endregion Read

        #region Delete

        [Test]
        public void DeleteOne()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = SUT.DeleteOne<T, TKey>(document);
            // Assert
            Assert.AreEqual(1, result);
            Assert.IsFalse(SUT.Any<T, TKey>(e => e.Id.Equals(document.Id), PartitionKey), GetTestName());
        }

        [Test]
        public void DeleteOneLinq()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = SUT.DeleteOne<T, TKey>(e => e.Id.Equals(document.Id), PartitionKey);
            // Assert
            Assert.AreEqual(1, result);
            Assert.IsFalse(SUT.Any<T, TKey>(e => e.Id.Equals(document.Id), PartitionKey), GetTestName());
        }

        [Test]
        public async Task DeleteOneAsync()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = await SUT.DeleteOneAsync<T, TKey>(document);
            // Assert
            Assert.AreEqual(1, result);
            Assert.IsFalse(SUT.Any<T, TKey>(e => e.Id.Equals(document.Id), PartitionKey), GetTestName());
        }

        [Test]
        public async Task DeleteOneAsyncLinq()
        {
            // Arrange
            var document = CreateTestDocument();
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = await SUT.DeleteOneAsync<T, TKey>(e => e.Id.Equals(document.Id), PartitionKey);
            // Assert
            Assert.AreEqual(1, result);
            Assert.IsFalse(SUT.Any<T, TKey>(e => e.Id.Equals(document.Id), PartitionKey), GetTestName());
        }

        [Test]
        public async Task DeleteManyAsyncLinq()
        {
            // Arrange
            var criteria = $"{GetTestName()}.{DocumentTypeName}";
            var documents = CreateTestDocuments(5);
            documents.ForEach(e => e.SomeContent = criteria);
            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = await SUT.DeleteManyAsync<T, TKey>(e => e.SomeContent == criteria, PartitionKey);
            // Assert
            Assert.AreEqual(5, result);
            Assert.IsFalse(SUT.Any<T, TKey>(e => e.SomeContent == criteria, PartitionKey), GetTestName());
        }

        [Test]
        public async Task DeleteManyAsync()
        {
            // Arrange
            var criteria = $"{GetTestName()}.{DocumentTypeName}";
            var documents = CreateTestDocuments(5);
            documents.ForEach(e => e.SomeContent = criteria);
            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = await SUT.DeleteManyAsync<T, TKey>(documents);
            // Assert
            Assert.AreEqual(5, result);
            Assert.IsFalse(SUT.Any<T, TKey>(e => e.SomeContent == criteria, PartitionKey), GetTestName());
        }

        [Test]
        public void DeleteManyLinq()
        {
            // Arrange
            var criteria = $"{GetTestName()}.{DocumentTypeName}";
            var documents = CreateTestDocuments(5);
            documents.ForEach(e => e.SomeContent = criteria);
            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = SUT.DeleteMany<T, TKey>(e => e.SomeContent == criteria, PartitionKey);
            // Assert
            Assert.AreEqual(5, result);
            Assert.IsFalse(SUT.Any<T, TKey>(e => e.SomeContent == criteria, PartitionKey), GetTestName());
        }

        [Test]
        public void DeleteMany()
        {
            // Arrange
            var criteria = $"{GetTestName()}.{DocumentTypeName}";
            var documents = CreateTestDocuments(5);
            documents.ForEach(e => e.SomeContent = criteria);
            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = SUT.DeleteMany<T, TKey>(documents);
            // Assert
            Assert.AreEqual(5, result);
            Assert.IsFalse(SUT.Any<T, TKey>(e => e.SomeContent == criteria, PartitionKey), GetTestName());
        }

        #endregion Delete

        #region Project

        [Test]
        public async Task ProjectOneAsync()
        {
            // Arrange
            var someContent = GetContent();
            var someDate = DateTime.UtcNow;
            var document = CreateTestDocument();
            document.SomeContent = someContent;
            document.Nested.SomeDate = someDate;
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = await SUT.ProjectOneAsync<T, MyTestProjection, TKey>(
                x => x.Id.Equals(document.Id),
                x => new MyTestProjection
                {
                    SomeContent = x.SomeContent,
                    SomeDate = x.Nested.SomeDate
                },
                PartitionKey);
            // Assert
            Assert.IsNotNull(result, GetTestName());
            Assert.AreEqual(someContent, result.SomeContent, GetTestName());
            Assert.AreEqual(someDate.Minute, result.SomeDate.Minute, GetTestName());
            Assert.AreEqual(someDate.Second, result.SomeDate.Second, GetTestName());
        }

        [Test]
        public void ProjectOne()
        {
            // Arrange
            var someContent = GetContent();
            var someDate = DateTime.UtcNow;
            var document = CreateTestDocument();
            document.SomeContent = someContent;
            document.Nested.SomeDate = someDate;
            SUT.AddOne<T, TKey>(document);
            // Act
            var result = SUT.ProjectOne<T, MyTestProjection, TKey>(
                x => x.Id.Equals(document.Id),
                x => new MyTestProjection
                {
                    SomeContent = x.SomeContent,
                    SomeDate = x.Nested.SomeDate
                },
                PartitionKey);
            // Assert
            Assert.IsNotNull(result, GetTestName());
            Assert.AreEqual(someContent, result.SomeContent, GetTestName());
            Assert.AreEqual(someDate.Minute, result.SomeDate.Minute, GetTestName());
            Assert.AreEqual(someDate.Second, result.SomeDate.Second, GetTestName());
        }

        [Test]
        public async Task ProjectManyAsync()
        {
            // Arrange
            var someContent = GetContent();
            var someDate = DateTime.UtcNow;
            var documents = CreateTestDocuments(5);
            documents.ForEach(e =>
            {
                e.SomeContent = someContent;
                e.Nested.SomeDate = someDate;
            });

            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = await SUT.ProjectManyAsync<T, MyTestProjection, TKey>(
                x => x.SomeContent == someContent,
                x => new MyTestProjection
                {
                    SomeContent = x.SomeContent,
                    SomeDate = x.Nested.SomeDate
                },
                PartitionKey);
            // Assert
            Assert.AreEqual(5, result.Count, GetTestName());
            Assert.AreEqual(someContent, result.First().SomeContent, GetTestName());
            Assert.AreEqual(someDate.Minute, result.First().SomeDate.Minute, GetTestName());
            Assert.AreEqual(someDate.Second, result.First().SomeDate.Second, GetTestName());
        }

        [Test]
        public void ProjectMany()
        {
            // Arrange
            var someContent = GetContent();
            var someDate = DateTime.UtcNow;
            var documents = CreateTestDocuments(5);
            documents.ForEach(e =>
            {
                e.SomeContent = someContent;
                e.Nested.SomeDate = someDate;
            });

            SUT.AddMany<T, TKey>(documents);
            // Act
            var result = SUT.ProjectMany<T, MyTestProjection, TKey>(
                x => x.SomeContent == someContent,
                x => new MyTestProjection
                {
                    SomeContent = x.SomeContent,
                    SomeDate = x.Nested.SomeDate
                },
                PartitionKey);
            // Assert
            Assert.AreEqual(5, result.Count, GetTestName());
            Assert.AreEqual(someContent, result.First().SomeContent, GetTestName());
            Assert.AreEqual(someDate.Minute, result.First().SomeDate.Minute, GetTestName());
            Assert.AreEqual(someDate.Second, result.First().SomeDate.Second, GetTestName());
        }

        #endregion Project

        #region Test Utils
        [MethodImpl(MethodImplOptions.NoInlining)]
        private string GetCurrentMethod()
        {
            StackTrace st = new StackTrace();
            StackFrame sf = st.GetFrame(1);

            return sf.GetMethod().Name;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private string GetParentMethod()
        {
            StackTrace st = new StackTrace();
            StackFrame sf = st.GetFrame(2);
            var method = sf.GetMethod().DeclaringType.Name;
            return method;
        }

        private string GetTestName()
        {
            return $"{TestClassName}{PartitionKey}.{GetParentMethod()}";
        }

        private string GetContent()
        {
            return $"{TestClassName}{PartitionKey}.{Guid.NewGuid()}.{GetParentMethod()}";
        }

        #endregion Test Utils
    }
}