﻿using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MvcMovie.DataAccess;
using MvcMovie.Models;
using System.Net;

namespace MvcMovie.FeatureTests
{
    [Collection("Controller Tests")]
    public class MovieControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public MovieControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        private MvcMovieContext GetDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<MvcMovieContext>();
            optionsBuilder.UseInMemoryDatabase("TestDatabase");

            var context = new MvcMovieContext(optionsBuilder.Options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            return context;
        }

        [Fact]
        public async Task Index_ReturnsViewWithMovies()
        {
            var context = GetDbContext();
            context.Movies.Add(new Movie { Genre = "Comedy", Title = "Spaceballs" });
            context.Movies.Add(new Movie { Genre = "Comedy", Title = "Young Frankenstein" });
            context.SaveChanges();

            var client = _factory.CreateClient();
            var response = await client.GetAsync("/Movies");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Spaceballs", html);
            Assert.Contains("Young Frankenstein", html);
            Assert.DoesNotContain("Elf", html);
        }

        [Fact]
        public async Task New_ReturnsFormView()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/movies/new");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Contains("Add a Movie", html);
            Assert.Contains("<form method=\"post\" action=\"/movies\">", html);
        }

        [Fact]
        public async Task AddMovie_ReturnsRedirectToShow()
        {
            // Context is only needed if you want to assert against the database
            var context = GetDbContext();

            // Arrange
            var client = _factory.CreateClient();
            var formData = new Dictionary<string, string>
            {
                { "Title", "Back to the Future" },
                { "Genre", "Science Fiction" }
            };

            // Act
            var response = await client.PostAsync("/movies", new FormUrlEncodedContent(formData));
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Movie Details", html);
            Assert.Contains("Title: Back to the Future", html);
            Assert.Contains("Genre: Science Fiction", html);

            // Assert that the movie was added to the database. In this situation the test is a bit redundant, but testing against what's in the database is a usefull testing tool to add to your toolbox.
            var savedMovie = context.Movies.FirstOrDefault(m => m.Title == "Back to the Future");
            Assert.NotNull(savedMovie);
            Assert.Equal("Science Fiction", savedMovie.Genre);
        }

        [Fact]
        public async Task Edit_ReturnsFormViewPrePopulated()
        {
            // Arrange
            var context = GetDbContext();
            var client = _factory.CreateClient();

            Movie movie = new Movie { Title = "Spaceballs", Genre = "Comedy" };
            context.Movies.Add(movie);
            context.SaveChanges();

            // Act
            var response = await client.GetAsync($"/movies/{movie.Id}/edit");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Edit Movie", html);
            Assert.Contains(movie.Title, html);
            Assert.Contains(movie.Genre, html);
        }

        [Fact]
        public async Task Update_SavesChangesToMovie()
        {
            // Arrange
            var context = GetDbContext();
            var client = _factory.CreateClient();

            Movie movie = new Movie { Title = "Goofy", Genre = "Comedy" };
            context.Movies.Add(movie);
            context.SaveChanges();

            var formData = new Dictionary<string, string>
            {
                { "Title", "Goofy" },
                { "Genre", "Documentary" }
            };

            // Act
            var response = await client.PostAsync(
                $"/movies/{movie.Id}",
                new FormUrlEncodedContent(formData)
            );
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Contains("Goofy", html);
            Assert.Contains("Documentary", html);
            Assert.DoesNotContain("Comedy", html);
        }

        [Fact]
        public async Task Delete_RemovesMovieFromIndexPage()
        {
            // Arrange
            var context = GetDbContext();
            var client = _factory.CreateClient();

            Movie movie = new Movie { Title = "Goofy", Genre = "Comedy" };
            context.Movies.Add(movie);
            context.SaveChanges();

            // Act
            var response = await client.PostAsync($"/movies/delete/{movie.Id}", null);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.DoesNotContain("Goofy", html);
        }

        [Fact]
        public async Task Delete_OnlyDeletesOneMovie()
        {
            // Arrange
            var context = GetDbContext();
            var client = _factory.CreateClient();

            Movie movieGoofy = new Movie { Title = "Goofy", Genre = "Comedy" };
            context.Movies.Add(movieGoofy);
            Movie movieElf = new Movie { Title = "Elf", Genre = "Holiday" };
            context.Movies.Add(movieElf);
            context.SaveChanges();

            // Act
            var response = await client.PostAsync($"/movies/delete/{movieGoofy.Id}", null);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Contains("Elf", html);
        }

        [Fact]
        public async Task Delete_RemovesAllDeletedMoviesReviews()
        {
            // Arrange
            var context = GetDbContext();
            var client = _factory.CreateClient();

            Movie spaceballs = new Movie { Genre = "Comedy", Title = "Spaceballs" };
            Review review = new Review
            {
                Content = "Great",
                Rating = 4,
                Movie = spaceballs
            };
            Review review2 = new Review
            {
                Content = "Just ok",
                Rating = 2,
                Movie = spaceballs
            };
            context.Movies.Add(spaceballs);
            context.Reviews.Add(review);
            context.Reviews.Add(review2);
            context.SaveChanges();

            // Act
            var response = await client.PostAsync($"/movies/delete/{spaceballs.Id}", null);

            // Assert
            var savedMovie = context.Reviews.FirstOrDefault(r => r.Movie == spaceballs);
            Assert.Null(savedMovie);
        }
    }
}
