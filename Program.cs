using Dapper;
using Npgsql;
using OneOf;
using OneOf.Types;

var connectionString = "Host=localhost;Username=postgres;Password=mysecretpassword;Database=postgres";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/todos", () =>
{
    using (var connection = new NpgsqlConnection(connectionString))
    {
        var sql = "SELECT * FROM todos";
        return connection.Query<Todo>(sql);
    }
})
.WithName("GetTodos")
.Produces(StatusCodes.Status200OK);

app.MapGet("/todos/{id}", (Guid id) =>
{
    using (var connection = new NpgsqlConnection(connectionString))
    {
        var sql = "SELECT * FROM todos WHERE id = @Id";
        var result = connection.Query<OneOf<Todo, TodoNotFound>>(sql, new { Id = id })
            .DefaultIfEmpty(new TodoNotFound(id))
            .First();

        return result.Match(
            todo => Results.Json(todo),
            todoNotFound => Results.NotFound(todoNotFound)
        );
    }
})
.WithName("GetTodoById")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/todos", (CreateTodo createTodo) =>
{
    using (var connection = new NpgsqlConnection(connectionString))
    {
        var sql = "INSERT INTO todos (text) VALUES (@Text) RETURNING *;";
        var todo = connection.QueryFirst<Todo>(sql, new { Text = createTodo.Text });
        return Results.Created($"/todos/{todo.Id}", todo);
    }
})
.WithName("PostTodo")
.Produces(StatusCodes.Status201Created);

app.MapDelete("/todos/{id}", (Guid id) =>
{
    using (var connection = new NpgsqlConnection(connectionString))
    {
        var sql = "DELETE FROM todos WHERE id = @Id;";
        OneOf<Some, TodoNotFound> result = connection.Execute(sql, new { Id = id }) switch
        {
            0 => new TodoNotFound(id),
            _ => new Some()
        };

        return result.Match(
            some => Results.NoContent(),
            todoNotFound => Results.NotFound(todoNotFound)
        );
    }
})
.WithName("DeleteTodo")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/todos/{id}/check", (Guid id) =>
{
    using (var connection = new NpgsqlConnection(connectionString))
    {
        var sql = "UPDATE todos SET \"isChecked\" = true WHERE id = @ID;";
        OneOf<Some, TodoNotFound> result = connection.Execute(sql, new { Id = id }) switch
        {
            0 => new TodoNotFound(id),
            _ => new Some()
        };

        return result.Match(
            some => Results.NoContent(),
            todoNotFound => Results.NotFound(todoNotFound)
        );
    }
})
.WithName("CheckTodo")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

app.MapDelete("/todos/{id}/check", (Guid id) =>
{
    using (var connection = new NpgsqlConnection(connectionString))
    {
        var sql = "UPDATE todos SET \"isChecked\" = false WHERE id = @ID;";
        OneOf<Some, TodoNotFound> result = connection.Execute(sql, new { Id = id }) switch
        {
            0 => new TodoNotFound(id),
            _ => new Some()
        };

        return result.Match(
            some => Results.NoContent(),
            todoNotFound => Results.NotFound(todoNotFound)
        );
    }
})
.WithName("UncheckTodo")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);
app.Run();

record Todo
{
    public Guid Id { get; init; }
    public string Text { get; init; } = "";
    public bool isChecked { get; init; }
    public DateTime createdAt { get; init; }
    public DateTime updatedAt { get; init; }
}

record CreateTodo
{
    public string Text { get; init; } = "";
}

abstract record Error
{
    public abstract string Message { get; }
}

record TodoNotFound : Error
{
    private Guid Id { get; init; }

    public TodoNotFound(Guid Id)
    {
        this.Id = Id;
    }

    public override string Message => $"Todo with ID {Id} could not be found.";
}