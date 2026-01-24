

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents; // Add this
using Amazon.S3;
using ImageUploader.Logic;

[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]


namespace ImageUploader.Api;

public class Functions
{
    private readonly IAmazonS3 _s3Client;

    public Functions(IAmazonS3 s3Client) => _s3Client = s3Client;

    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Post, "/upload/{fileName}")]
    // Change Stream body to APIGatewayHttpApiV2ProxyRequest request
    public async Task<IHttpResult> UploadHandler(string fileName, APIGatewayHttpApiV2ProxyRequest request)
    {
        try
        {
            // Convert the body string to a MemoryStream
            var bytes = request.IsBase64Encoded 
                ? Convert.FromBase64String(request.Body) 
                : System.Text.Encoding.UTF8.GetBytes(request.Body);

            using var ms = new MemoryStream(bytes);

            // Call F# Logic
            await BucketService.uploadImage(_s3Client, fileName, ms);
            await BucketService.updateXmlMetadata(_s3Client, fileName);

            return HttpResults.Ok($"Uploaded {fileName} is now in the bucket and XML is updated");
        }
        catch (Exception ex)
        {
            return HttpResults.InternalServerError(ex.Message);
        }
    }


[LambdaFunction]
[HttpApi(LambdaHttpMethod.Delete, "/delete/{fileName}")]
public async Task<IHttpResult> DeleteHandler(string fileName)
{
    try {
        bool success = await BucketService.deleteImage(_s3Client, fileName);
        return success 
            ? HttpResults.Ok($"Deleted {fileName}") 
            : HttpResults.NotFound("File not found in XML");
    }
    catch (Exception ex) {
        return HttpResults.InternalServerError(ex.Message);
    }
    }


}
