using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;

public class SNSNotificationEmail
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IAmazonS3 _s3Client;
    private readonly string _snsTopicArn;
    private readonly string _s3BucketName;
    private readonly string _s3Key;

    public SNSNotificationEmail(string snsTopicArn, string s3BucketName, string s3Key)
    {
        _snsClient = new AmazonSimpleNotificationServiceClient();
        _s3Client = new AmazonS3Client();
        _snsTopicArn = snsTopicArn;
        _s3BucketName = s3BucketName;
        _s3Key = s3Key;
    }

    public async Task PublishSNSNotificationAsync(string message)
    {
        var s3Object = await GetS3ObjectAsync();

        var attachment = new Attachment(s3Object.ResponseStream, s3Object.Key);
        var mailMessage = new MailMessage
        {
            Subject = "Infrastructure Damage Alert",
            Body = message,
            From = new MailAddress("narayana.sambashivarao@gmail.com"),
            IsBodyHtml = true
        };

        mailMessage.Attachments.Add(attachment);

        var request = new PublishRequest
        {
            TopicArn = _snsTopicArn,
            Subject = "Infrastructure Damage Alert",
            Message = message
        };

        try
        {
            await _snsClient.PublishAsync(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task<GetObjectResponse> GetS3ObjectAsync()
    {
        var request = new GetObjectRequest
        {
            BucketName = _s3BucketName,
            Key = _s3Key
        };

        var response = await _s3Client.GetObjectAsync(request);

        return response;
    }
}

