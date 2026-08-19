using PictureTool.Services;
using Xunit;

namespace PictureTool.Tests;

public class OcrPostProcessTests
{
    [Theory]
    [InlineData("待 办 中 心", "待办中心")]
    [InlineData("服 务 端", "服务端")]
    [InlineData("运 维 后 台", "运维后台")]
    [InlineData("hello world", "hello world")]
    [InlineData("中文 English 中文", "中文 English 中文")]
    [InlineData("纯中文无空格", "纯中文无空格")]
    public void RemoveCjkSpaces_RemovesSpacesBetweenCjkOnly(string input, string expected)
    {
        Assert.Equal(expected, OcrService.RemoveCjkSpaces(input));
    }

    [Theory]
    [InlineData("企业  Web 端", "企业 Web 端")]
    [InlineData("测试  ABC  结果", "测试 ABC 结果")]
    [InlineData("正常 A 正常", "正常 A 正常")]
    [InlineData("（ 括号）", "（括号）")]
    public void NormalizeCjkLatinSpaces_CompressesExtraSpaces(string input, string expected)
    {
        Assert.Equal(expected, OcrService.NormalizeCjkLatinSpaces(input));
    }

    [Theory]
    [InlineData("〔服务端〕", "[服务端]")]
    [InlineData("臼名单", "白名单")]
    [InlineData("审扌比", "审批")]
    [InlineData("正常文字", "正常文字")]
    public void FixCommonMisrecognitions_FixesKnownErrors(string input, string expected)
    {
        Assert.Equal(expected, OcrService.FixCommonMisrecognitions(input));
    }

    [Theory]
    [InlineData("文字 ，后面", "文字，后面")]
    [InlineData("句子 。结束", "句子。结束")]
    [InlineData("正常，标点", "正常，标点")]
    public void NormalizePunctuation_RemovesSpacesBeforePunctuation(string input, string expected)
    {
        Assert.Equal(expected, OcrService.NormalizePunctuation(input));
    }

    [Theory]
    [InlineData("待 办 中 心 〔 服 务 端 〕", "待办中心[服务端]")]
    [InlineData("审 扌比 跨 平 台", "审批跨平台")]
    public void PostProcess_CombinesAllSteps(string input, string expected)
    {
        Assert.Equal(expected, OcrService.PostProcess(input));
    }
}
