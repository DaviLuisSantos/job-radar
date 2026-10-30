using System.Text.Json;

namespace JobRadar.Web.Text;

/// <summary>
/// The "Send to JobRadar" bookmarklet. It runs on a Seek ad page the candidate opened themselves:
/// takes the ad text (or what they selected), copies it to the clipboard, opens the capture page of
/// this site and, once that page says it is listening, sends it the text. The clipboard is the way
/// back when the browser keeps the two windows apart; this site never opens a Seek ad page.
/// </summary>
public static class Bookmarklet
{
    /// <summary>Seek's ad body; a selection wins, for when the page changes.</summary>
    internal const string AdSelector = "[data-automation=jobAdDetails]";

    /// <param name="origin">This site as the browser reaches it, e.g. <c>http://localhost:5380</c>.</param>
    public static string For(string origin)
    {
        var o = JsonSerializer.Serialize(origin.TrimEnd('/'));
        return "javascript:(()=>{"
            + $"const o={o};"
            + @"const m=location.href.match(/\/job\/(\d+)/)||location.href.match(/[?&]jobId=(\d+)/);"
            + $"if(!m){{alert('{Js("JobRadar: abra antes um anúncio de vaga no Seek.")}');return;}}"
            + "const s=String(getSelection()).trim();"
            + $"const d=document.querySelector('{AdSelector}');"
            + "const t=s||(d?d.innerText:'');"
            + $"if(t.length<80){{alert('{Js("JobRadar: texto do anúncio não encontrado. Selecione o texto e clique de novo.")}');return;}}"
            + "if(navigator.clipboard)navigator.clipboard.writeText(t).catch(()=>{});"
            + "const w=window.open(o+'/capture?source=seek&id='+m[1],'jobradar');"
            + $"if(!w){{alert('{Js("JobRadar: permita pop-ups nesta página.")}');return;}}"
            + "const f=e=>{if(e.origin===o&&e.data==='jobradar-ready'){"
            + "w.postMessage({type:'jobradar-capture',source:'seek',id:m[1],text:t},o);removeEventListener('message',f);}};"
            + "addEventListener('message',f);"
            + "})()";
    }

    /// <summary>A message for a single-quoted JavaScript string, accents as <c>\uXXXX</c>: the bookmark stays plain ASCII.</summary>
    private static string Js(string text) =>
        string.Concat(text.Select(c => c < 128 ? c.ToString() : $"\\u{(int)c:x4}"));
}
