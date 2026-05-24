using System;

namespace TypeSunny.Core
{
    public enum TypingState
    {
        typing,
        pause,
        ready,
        end
    }

    public enum RetypeType
    {
        first,

        retype,
        shuffle,
        wrongRetype,
        slowRetype,
    }
    public enum TxtSource
    {
       unchange,
        qq,
        clipboard,
        changeSheng,
        jbs,
        jisucup,
        book,
        trainer,
        articlesender,
        raceApi
    }
    static internal class StateManager
    {
        static public string Version = "晴跟打";
        static public bool TextInput = false;

        static public bool ConfigLoaded = false;

        static public bool LastType = false;


        static public TypingState typingState = TypingState.ready;


        static public TxtSource txtSource = TxtSource.unchange;
        static public RetypeType retypeType = RetypeType.first;

        // 赛文服务器信息（用于多服务器支持）
        static public string CurrentRaceServerId = "";
        static public int CurrentRaceId = -1;
        //       static public bool IsChangSheng = false;

        // 最后一次有效输入的时间（用于赛文字提5秒限制）
        static public DateTime LastInputTime = DateTime.Now;

        // 是否处于赛文模式（锦标赛/极速杯/赛文API 任一）
        // 赛文模式下需要强制关闭词提、字提编码下显、字提选重角标，并对上方字提行启用 5 秒延迟
        static public bool IsRaceMode()
        {
            return txtSource == TxtSource.raceApi
                || txtSource == TxtSource.jbs
                || txtSource == TxtSource.jisucup;
        }
    }
}
