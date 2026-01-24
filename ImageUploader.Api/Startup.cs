using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Amazon.S3;
using Amazon.Extensions.NETCore.Setup;

namespace ImageUploader.Api;


[Amazon.Lambda.Annotations.LambdaStartup]
public class Startup
{
    
    public void ConfigureServices(IServiceCollection services)
    {
        // This adds the S3 Client to the DI container
       services.AddAWSService<IAmazonS3>();

        
    }
}
