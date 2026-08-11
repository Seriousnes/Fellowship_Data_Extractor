namespace FellowshipExtractor.Api.Endpoints;

public static class Origins
{
    public static string Of(HttpContext http) =>
        $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}";
}
