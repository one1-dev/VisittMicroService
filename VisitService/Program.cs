using Newtonsoft.Json.Converters;
using VisitService.CModels;
using VisitService.Middleware;
using VisitService.Repos;
using VisitService.Services;
using VisitService.Services.Implementations;
using VisitService.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.Converters.Add(new StringEnumConverter());
    });

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", builder =>
    {
        builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.Configure<VisittApiOptions>(builder.Configuration.GetSection("VisittApi"));
builder.Services.AddSingleton<GraphQlClient>(); 
builder.Services.AddScoped(typeof(GenericGraphQlService<>));
builder.Services.AddScoped<IWorkOrdersService, WorkOrdersService>();
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<ISitesService, SitesService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<ITenantService, TenantService>();





builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();  
app.UseAuthorization(); 

app.UseRouting();
app.MapControllers(); 
app.Run();
