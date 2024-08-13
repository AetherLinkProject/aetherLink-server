using System;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
using AetherLink.AIServer.Core.Dtos;
using AetherLink.AIServer.Core.Options;
using AetherLink.AIServer.Core.Providers;
using AetherLink.Contracts.AIFeeds;
using Ai;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.AIServer.Core.Enclave;

public interface IEnclaveManager
{
    Task<RequestStorageDto> CreateAsync(AIRequestDto request);
    Task<(ByteString, byte[])> ProcessAsync(OracleContext ctx, RequestStorageDto data);
    Task FinishAsync(AIReportTransmittedDto transmitted);
}

public class EnclaveManager : IEnclaveManager, ISingletonDependency
{
    private readonly EnclaveOption _option;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<EnclaveManager> _logger;
    private readonly IOpenAIProvider _openAIProvider;
    private readonly IRequestProvider _requestProvider;

    public EnclaveManager(ILogger<EnclaveManager> logger, IOpenAIProvider openAiProvider,
        IOptions<EnclaveOption> option,
        IRequestProvider requestProvider,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _option = option.Value;
        _objectMapper = objectMapper;
        _openAIProvider = openAiProvider;
        _requestProvider = requestProvider;
    }

    public async Task<RequestStorageDto> CreateAsync(AIRequestDto request)
    {
        var data = _objectMapper.Map<AIRequestDto, RequestStorageDto>(request);
        var id = GenerateRequestId(request);
        data.Id = id;
        data.Status = RequestType.Started;
        await _requestProvider.SetAsync(id, data);

        return data;
    }

    public async Task FinishAsync(AIReportTransmittedDto transmitted)
    {
        var id = GenerateRequestId(transmitted);
        var request = await _requestProvider.GetAsync(id);
        request.Status = RequestType.Finished;
        await _requestProvider.SetAsync(id, request);
    }

    public async Task<(ByteString, byte[])> ProcessAsync(OracleContext ctx, RequestStorageDto data)
    {
        var r = AIRequest.Parser.ParseFrom(ByteString.FromBase64(data.Commitment));
        _logger.LogDebug($"[EnclaveManager] {r.Description.Detail.ToStringUtf8()}");

        ByteString report;
        switch (r.Model)
        {
            case ModelType.ChatGpt:
                var chatGptResponse = await _openAIProvider.GetCompletionAsync(new()
                {
                    Model = "gpt-4o",
                    Messages = new()
                    {
                        new()
                        {
                            Role = "user",
//                             Content = @"
// 目标：请参考用户的输入，设计一个游戏中的boss，并输出以下参数，并讲参数组合成一个boss背景介绍。
// 最终输出参数格式为：
// {
//     外观：#毛色{}、#大小{}、#性格{}、#体重{}、#眼睛颜色{}、#其他特殊标记或特征{}, %稀有度评分{}；
//     整体人物外观描述：#描述{}
//     属性：#敏捷{} 、#力量{}、 #智力{}；
//     宝物：{}
// }
// 具体规则如下：
// 1. 外观设计：
//   1. 请根据用户提供的外观特征，创造性地设计这只boss，具体格式为#毛色、#大小、#性格、#体重、#眼睛颜色、#其他特殊标记或特征, %稀有度评分。
//   2. 其中稀有度评分1到5。评分应该基于你自己的喜好进行，并且喜好越高，稀有度越高。记住，得分要有一定随机性，基于你的喜好，大概50% 上下的得分漂移。
// 2. 属性：
//   1. 请根据用户提供的属性描述，进行如下属性设计
//     1. 敏捷  范围{20~50}
//     2. 力量  范围{20~50}
//     3. 智力 范围{20~50}
//     说明。敏捷+力量+智力最后加起来 要等于100。
//     另外，还有如下属性
//     1. 基础伤害/攻速，范围{100~299}，但是基础伤害越高，攻速越低
//     2. 大招属性，范围{10000~39999}，但是基础伤害越高，大招蓄力时间越长
//     3. 血量 ：范围{5000000~199999999}，
// 3. 请根据用户提供的希望掉落宝物描述，设计该boss死后掉落的宝物，从下面中选择{战士之盾，法师斗篷，盗贼匕首}
//
// 以下就是用户输入：
// - 外观特征: 设计的漂亮一点就行，女性角色
// - boss的能力描述： 敏捷为主，大招最好是厉害一点，如果是闪电就好了
// - 期望boss掉落宝物：我想要一个战士的武器
// "
                            Content = r.Description.Detail.ToStringUtf8()
                        }
                    }
                });

                report = new ChatGptResponse
                {
                    Model = chatGptResponse.Model,
                    Content = ByteString.CopyFromUtf8(chatGptResponse.Choices[0].Message.Content)
                }.ToByteString();

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var signedResult = SignReport(report, ctx);
        data.Status = RequestType.Signed;
        await _requestProvider.SetAsync(data.Id, data);
        return (report, signedResult);
    }

    private byte[] SignReport(ByteString report, OracleContext ctx)
    {
        var payload = HashHelper.ConcatAndCompute(
            HashHelper.ComputeFrom(report.ToByteArray()),
            HashHelper.ComputeFrom(ctx.ToString())).ToByteArray();
        return CryptoHelper.SignWithPrivateKey(ByteArrayHelper.HexStringToByteArray(_option.PrivateKey), payload);
    }

    private string GenerateRequestId(AIRequestDto data) => data.ChainId + "-" + data.RequestId;
    private string GenerateRequestId(AIReportTransmittedDto data) => data.ChainId + "-" + data.RequestId;
}