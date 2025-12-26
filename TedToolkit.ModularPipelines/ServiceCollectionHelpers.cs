// -----------------------------------------------------------------------
// <copyright file="ServiceCollectionHelpers.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using GeminiDotnet;
using GeminiDotnet.Extensions.AI;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using TedToolkit.ModularPipelines.Options;

namespace TedToolkit.ModularPipelines;

/// <summary>
/// 用于处理服务工具的帮助器
/// </summary>
public static class ServiceCollectionHelpers
{
    /// <summary>
    /// 增加AI的内容
    /// </summary>
    /// <param name="collection">服务集合</param>
    /// <param name="context">环境</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAi(this IServiceCollection collection, HostBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        collection.Configure<AiPipelineOptions>(context.Configuration.GetSection("AI"));

        collection.AddSingleton<IChatClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AiPipelineOptions>>();
            var option = new GeminiClientOptions() { ApiKey = options.Value.ApiKey, };
            if (!string.IsNullOrEmpty(options.Value.EndPoint))
                option.Endpoint = new(options.Value.EndPoint);

            if (!string.IsNullOrEmpty(options.Value.ModelId))
                option.ModelId = options.Value.ModelId;

            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(GeminiClient));
            httpClient.BaseAddress = option.Endpoint;
            httpClient.DefaultRequestHeaders.Add("x-goog-api-key", option.ApiKey);
            var geminiClient = new GeminiClient(httpClient);

            typeof(GeminiClient).GetRuntimeFields()
                .Single(f => f.FieldType == typeof(IGeminiClientOptions))
                .SetValue(geminiClient, option);

            return new GeminiChatClient(geminiClient);
        });

        collection.ConfigureHttpClientDefaults(builder =>
            builder.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(10)));

        return collection;
    }
}