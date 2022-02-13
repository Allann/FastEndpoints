﻿using Microsoft.AspNetCore.Routing;
using NJsonSchema;
using NSwag;
using NSwag.Generation.AspNetCore;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FastEndpoints.Swagger;

internal class DefaultOperationProcessor : IOperationProcessor
{
    private static readonly Regex regex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled);
    private static readonly Dictionary<string, string> defaultDescriptions = new()
    {
        { "200", "Success" },
        { "201", "Created" },
        { "202", "Accepted" },
        { "204", "No Content" },
        { "400", "Bad Request" },
        { "401", "Unauthorized" },
        { "403", "Forbidden" },
        { "404", "Not Found" },
        { "405", "Mehtod Not Allowed" },
        { "406", "Not Acceptable" },
        { "500", "Server Error" },
    };

    private readonly int tagIndex;
    public DefaultOperationProcessor(int tagIndex) => this.tagIndex = tagIndex;

    public bool Process(OperationProcessorContext ctx)
    {
        var op = ctx.OperationDescription.Operation;
        var metaData = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription.ActionDescriptor.EndpointMetadata;
        var epMeta = metaData.OfType<EndpointMetadata>().SingleOrDefault();

        if (epMeta is null)
            return true; //this is not a fastendpoint

        var brk = ctx.Document.Security.Count > 0;

        var apiVer = epMeta.EndpointSettings.Version.Current;
        var version = $"/{Config.VersioningOpts?.Prefix}{apiVer}";
        var routePrefix = "/" + (Config.RoutingOpts?.Prefix ?? "_");
        var bareRoute = ctx.OperationDescription.Path.Remove(routePrefix).Remove(version);
        var nameMetaData = metaData.OfType<EndpointNameMetadata>().LastOrDefault();

        if (nameMetaData is not null)
            op.OperationId = nameMetaData.EndpointName; //set operation id if user has specified

        if (tagIndex > 0) //set tag
        {
            var segments = bareRoute.Split('/');
            if (segments.Length >= tagIndex)
                op.Tags.Add(segments[tagIndex]);
        }

        op.Tags.Add($"|{ctx.OperationDescription.Method}:{bareRoute}|{apiVer}|{epMeta.EndpointSettings.Version.DeprecatedAt}"); //this will be later removed from document processor

        var reqContent = op.RequestBody?.Content;
        if (reqContent?.Count > 0)
        {
            //fix request content-type not displaying correctly. probably a nswag bug. might be fixed in future.
            var contentVal = reqContent.FirstOrDefault().Value;
            reqContent.Clear();
            reqContent.Add(op.Consumes.FirstOrDefault(), contentVal);
        }

        var resContent = op.Responses.FirstOrDefault().Value.Content;
        if (resContent?.Count > 0)
        {
            //fix response content-type not displaying correctly. probably a nswag bug. might be fixed in future.
            var contentVal = resContent.FirstOrDefault().Value;
            resContent.Clear();
            resContent.Add(op.Produces.FirstOrDefault(), contentVal);
        }

        //set response descriptions
        op.Responses
          .Where(r => string.IsNullOrWhiteSpace(r.Value.Description))
          .ToList()
          .ForEach(res =>
          {
              if (defaultDescriptions.ContainsKey(res.Key))
                  res.Value.Description = defaultDescriptions[res.Key]; //first set the default text

              if (epMeta.EndpointSettings.Summary is not null)
                  res.Value.Description = epMeta.EndpointSettings.Summary[Convert.ToInt32(res.Key)]; //then take values from summary object
          });

        //set endpoint summary & description
        op.Summary = epMeta.EndpointSettings.Summary?.Summary;
        op.Description = epMeta.EndpointSettings.Summary?.Description;

        var apiDescription = ((AspNetCoreOperationProcessorContext)ctx).ApiDescription;
        var reqDtoType = apiDescription.ParameterDescriptions.FirstOrDefault()?.Type;
        var reqDtoProps = reqDtoType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var isGETRequest = apiDescription.HttpMethod == "GET";

        //fix missing path parameters
        ctx.OperationDescription.Path = "/" + apiDescription.RelativePath;

        if (isGETRequest && op.RequestBody is not null)
        {
            //remove request body since this is a get request with a request dto,
            //cause swagger ui/fetch client doesn't support GET with body
            op.RequestBody = null;
        }

        var reqParams = new List<OpenApiParameter>();

        //add a param for each url path segment such as /{xxx}/{yyy}/{zzz}
        reqParams = regex
            .Matches(apiDescription?.RelativePath!)
            .Select(m => new OpenApiParameter
            {
                Name = m.Value,
                Kind = OpenApiParameterKind.Path,
                IsRequired = true,
                Schema = JsonSchema.FromType(typeof(string))
            })
            .ToList();

        if (isGETRequest && reqDtoType is not null)
        {
            //it's a GET request with a request dto
            //so let's add each dto property as a query param to enable swagger ui to execute GET request with user supplied values

            var qParams = reqDtoProps?
                .Where(p =>
                      !p.IsDefined(typeof(FromClaimAttribute), false) &&
                      !p.IsDefined(typeof(FromHeaderAttribute), false) &&
                      !reqParams.Any(rp => rp.Name == p.Name)) //ignore props marksed with [FromClaim],[FromHeader] or has a route param.
                .Select(p =>
                    new OpenApiParameter
                    {
                        Name = p.Name,
                        IsRequired = false,
                        Schema = JsonSchema.FromType(p.PropertyType),
                        Kind = OpenApiParameterKind.Query
                    })
                .ToList();

            if (qParams?.Count > 0)
                reqParams.AddRange(qParams);
        }

        if (reqDtoProps is not null)
        {
            //add header params if there are any props marked with [FromHeader] attribute
            foreach (var prop in reqDtoProps)
            {
                var attrib = prop.GetCustomAttribute<FromHeaderAttribute>(true);
                if (attrib is not null)
                {
                    reqParams.Add(new OpenApiParameter
                    {
                        Name = attrib?.HeaderName ?? prop.Name,
                        IsRequired = attrib?.IsRequired ?? false,
                        Schema = JsonSchema.FromType(prop.PropertyType),
                        Kind = OpenApiParameterKind.Header
                    });
                }
            }
        }

        foreach (var p in reqParams)
            op.Parameters.Add(p);

        return true;
    }
}