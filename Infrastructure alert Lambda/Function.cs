using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]

namespace WatchDogAlertSystem
{
    public class Function
    {
        private readonly IAmazonS3 s3Client;
        private readonly IAmazonRekognition rekognitionClient;
        private readonly IAmazonCloudWatchLogs _cloudWatchLogsClient;

        public Function()
        {
            s3Client = new AmazonS3Client();
            rekognitionClient = new AmazonRekognitionClient();
        }

        public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
        {
            context.Logger.LogLine($"Received S3 event: {JsonConvert.SerializeObject(s3Event)}");

            foreach (var record in s3Event.Records)
            {
                var s3Bucket = record.S3.Bucket.Name;
                var s3Key = record.S3.Object.Key;

                context.Logger.LogLine($"Processing S3 object: bucket={s3Bucket}, key={s3Key}");

                var getObjectRequest = new GetObjectRequest
                {
                    BucketName = s3Bucket,
                    Key = s3Key
                };

                using (var getObjectResponse = await s3Client.GetObjectAsync(getObjectRequest))
                {
                    using (var stream = getObjectResponse.ResponseStream)
                    {
                        var detectLabelsRequest = new DetectLabelsRequest
                        {
                            Image = new Amazon.Rekognition.Model.Image
                            {
                                //Bytes = ReadFully(stream)
                            },
                            MinConfidence = 80F
                        };

                        var detectLabelsResponse = await rekognitionClient.DetectLabelsAsync(detectLabelsRequest);

                        var damagedRoadLabels = detectLabelsResponse.Labels
                            .Where(label => label.Name.Equals("Pothole") || label.Name.Equals("Crack"))
                            .ToList();

                        if (damagedRoadLabels.Any())
                        {
                            context.Logger.LogLine($"Found damaged road labels: {JsonConvert.SerializeObject(damagedRoadLabels)}");

                            // Send alert notification to relevant parties
                            // ...
                        }
                        else
                        {
                            context.Logger.LogLine("No damaged road labels found");
                        }
                    }
                }
            }
        }

        public async Task CloudWatchFunctionHandler(string input, ILambdaContext context)
        {
            var logGroupName = "InfrastructureAlert";
            var logStreamName = $"InfrastructureAlert/{DateTime.UtcNow.ToString("yyyy/MM/dd/HH/mm/ss")}";
            var message = $"Input received: {input}";

            var request = new PutLogEventsRequest
            {
                LogGroupName = logGroupName,
                LogStreamName = logStreamName,
                LogEvents = new List<InputLogEvent>
            {
                new InputLogEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Message = message
                }
            }
            };

            var response = await _cloudWatchLogsClient.PutLogEventsAsync(request);

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Failed to write log event. StatusCode={response.HttpStatusCode}");
            }

            context.Logger.LogLine($"Log event written to CloudWatch Logs. LogGroupName={logGroupName}, LogStreamName={logStreamName}, Message={message}");
        }
    }

    private static byte[] ReadFully(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
