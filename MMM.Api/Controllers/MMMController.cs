using Markdig;
using Markdig.Parsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Identity.Web.Resource;
using System.Net;
using System.Reflection;
using System.Text.Encodings.Web;

namespace MMM.Api.Controllers
{
    [ApiController]
    [Route("[action]")]
    public class MMMController : ControllerBase
    {
        private readonly MessagesDbContext _messagesDbContext;
        public MMMController(MessagesDbContext messagesDbContext)
        {
            _messagesDbContext = messagesDbContext;
        }

        [HttpGet]
        public async Task<IEnumerable<Message>> GetAllMessages()
        {
            var result = await _messagesDbContext.Messages.ToListAsync();
            return result;
        }

        [HttpGet]
        public async Task<IEnumerable<string?>> GetDistinctModules()
        {
            var result = await _messagesDbContext.Messages.Select(x => x.ModuleName ?? x.AssemblyName).Distinct().ToListAsync();
            return result;
        }

        [HttpGet]
        public async Task<IEnumerable<string?>> GetDistinctMessages()
        {
            var result = await _messagesDbContext.Messages.Select(x => x.MessageName).Distinct().ToListAsync();
            return result;
        }

        [HttpPost]
        [Consumes("text/plain")]
        public async Task PostMessage([FromQuery] string? direction, [FromQuery] string? messageName, [FromBody] string? messageSchema, [FromQuery] string? messageDescription, [FromQuery] string? moduleName, [FromQuery] string? assemblyName, [FromQuery] string? compilationId, [FromQuery] string? location)
        {
            await _messagesDbContext.Messages.Where(x => x.AssemblyName == assemblyName && x.CompilationId != compilationId).ExecuteDeleteAsync();
            await _messagesDbContext.Messages.AddAsync(new Message { Direction = direction?.Trim(), MessageName = messageName?.Trim(), MessageSchema = messageSchema?.Trim(), MessageDescription = messageDescription?.Trim(), ModuleName = moduleName?.Trim(), AssemblyName = assemblyName?.Trim(), CompilationId = compilationId?.Trim(), Location=location?.Trim(), CreationDate = DateTime.Now });
            await _messagesDbContext.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IActionResult> GetPlantUml([FromQuery] string[] modules)
        {
            var moduleMessages = _messagesDbContext.Messages.Where(x => modules.Any(m => m == (x.ModuleName ?? x.AssemblyName))).Select(x=>x.MessageName).Distinct();
            var messages = await _messagesDbContext.Messages.Where(x => moduleMessages.Any(m => m == x.MessageName)).ToListAsync();
            var topics = messages
                .GroupBy(obj => obj.MessageName)
                .Select(g => new
                {
                    MessageName = g.Key,
                    Producers = g.Where(o => o.Direction == "publish").Select(o => o.ModuleName ?? o.AssemblyName).DefaultIfEmpty("ANY"),
                    Consumers = g.Where(o => o.Direction == "subscribe").Select(o => o.ModuleName ?? o.AssemblyName).DefaultIfEmpty("ANY")
                })
                .ToList();
            var links = from topic in topics
                        from producer in topic.Producers
                        from consumer in topic.Consumers
                        select new { Link = topic.MessageName, From = producer, To = consumer };
            var distinctModules = links.Select(d => d.From).Union(links.Select(d => d.To)).Distinct().OrderBy(s => s);
            var plantUml = @$"
@startuml
skinparam usecase {{
  FontSize 24
  BackgroundColor gold
  ArrowFontSize 10
}}
skinparam cloud {{
  FontSize 24
  ArrowFontSize 10
}}
{string.Join("\n", distinctModules.Select(m => @$"{(modules.Contains(m) ? "usecase" : "cloud")} ""{m}"" as {m.Replace(" ", "")}"))}
{string.Join("\n", links.Select(l => $@"{l.From} --> {l.To} : {l.Link}"))}
@enduml";

            var code = PlantUmlHelper.EncodeP(plantUml);
            var svg = $@"https://www.plantuml.com/plantuml/svg/{code}";
            return Redirect(svg);
        }
        [HttpGet]
        public async Task<IActionResult> GetWiki([FromQuery] string[] modules)
        {
            var messages = await _messagesDbContext.Messages.Where(x => modules.Any(m => m == (x.ModuleName ?? x.AssemblyName))).ToListAsync();

            var messagesGroupedByModule = messages.GroupBy(t => t.ModuleName ?? t.AssemblyName);
            var wiki = string.Join("\n", messagesGroupedByModule.Where(g => !string.IsNullOrWhiteSpace(g.Key)).OrderBy(g => g.Key).Select(g => $@"
# {g.Key}{string.Join("\n", g.OrderBy(t => t.MessageName).Select(t => $@"
- **{t.MessageName}** ({t.Direction?.ToUpper()}){(string.IsNullOrWhiteSpace(t.MessageDescription) ? "" : $@"

{t.MessageDescription}")}{(string.IsNullOrWhiteSpace(t.MessageSchema) ? "" : $@"

```
{t.MessageSchema}
```
")}"))}

<img src=""{Url.ActionLink(nameof(GetPlantUml))}?modules={g.Key}"">
"));
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSyntaxHighlighting()
                .Build();
            var markdownHtml = Markdown.ToHtml(wiki, pipeline);
            var markdownStyle = ".markdown-content{color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));word-wrap:break-word;font-size:.9375rem}.markdown-content>*:first-child:not(.bolt-table){margin-top:0}.markdown-content>*:last-child:not(.bolt-table){margin-bottom:0}.markdown-content a{color:rgba(0,90,158,1);color:var(--communication-foreground,rgba(0, 90, 158, 1));text-decoration:none;outline:transparent}.markdown-content a:hover{color:rgba(0,69,120,1);color:rgba(var(--palette-primary-shade-30,0, 69, 120),1)}.markdown-content p,.markdown-content blockquote,.markdown-content ul,.markdown-content ol,.markdown-content table,.markdown-content pre{margin:0 0 16px 0}.markdown-content table:not(.bolt-table){border-collapse:collapse;border-spacing:0;color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));cursor:default;display:block;overflow-x:auto}.markdown-content table:not(.bolt-table) table{margin-bottom:0}.markdown-content table:not(.bolt-table) th{color:rgba(0,0,0,.55);color:var(--text-secondary-color,rgba(0, 0, 0, .55));font-size:.9375rem;font-weight:600;text-align:left;border:1px solid rgba(234,234,234,1);border:1px solid var(--component-grid-cell-bottom-border-color,rgba(234, 234, 234, 1));padding:13px 11px}.markdown-content table:not(.bolt-table) td{border:1px solid rgba(234,234,234,1);border:1px solid var(--component-grid-cell-bottom-border-color,rgba(234, 234, 234, 1));text-align:left;padding:10px 13px;min-width:50px;max-width:1000px}.markdown-content code{font-family:Menlo,Consolas,Courier New,monospace;font-size:.75rem;background-color:rgba(0,0,0,.06);background-color:var(--palette-black-alpha-6,rgba(0, 0, 0, .06));color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));padding:2px 4px;border-radius:2px}.markdown-content hr{border:1px solid rgba(234,234,234,1);border:1px solid var(--component-grid-cell-bottom-border-color,rgba(234, 234, 234, 1))}.markdown-content pre{font-size:.75rem;white-space:pre;word-wrap:normal;background-color:rgba(0,0,0,.06);background-color:var(--palette-black-alpha-6,rgba(0, 0, 0, .06));border-radius:2px;padding:15px;overflow-x:auto}.markdown-content pre code{font-size:inherit;background-color:transparent;padding:0}.markdown-content blockquote{color:rgba(0,0,0,.7);color:var(--palette-black-alpha-70,rgba(0, 0, 0, .7));border-left:2px solid rgba(234,234,234,1);border-left:2px solid var(--component-grid-cell-bottom-border-color,rgba(234, 234, 234, 1));padding-left:16px}.markdown-content blockquote>*:first-child{margin-top:0}.markdown-content blockquote>*:last-child{margin-bottom:0}.markdown-content blockquote>p,.markdown-content blockquote ol,.markdown-content blockquote ul{margin:16px 0}.markdown-content img{max-width:100%}.markdown-content ul,.markdown-content ol{padding-left:32px}.markdown-content ul ul,.markdown-content ul ol,.markdown-content ol ul,.markdown-content ol ol{margin-top:0;margin-bottom:0}.markdown-content li{list-style:inherit}.markdown-content li+li{margin-top:4px}.markdown-content h1{font-size:1.3125rem;font-weight:600;color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));margin:24px 0 8px 0;letter-spacing:-.04em}.markdown-content h1 .bowtie-icon{font-size:unset}.markdown-content h1:first-child{margin-top:0}.markdown-content h2{font-size:1.0625rem;font-weight:600;color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));margin:24px 0 8px 0}.markdown-content h2 .bowtie-icon{font-size:unset}.markdown-content h3{font-size:.9375rem;font-weight:600;color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));margin:24px 0 8px 0}.markdown-content h3 .bowtie-icon{font-size:unset}.markdown-content h4{font-size:.875rem;font-weight:600;color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));margin:24px 0 8px 0}.markdown-content h4 .bowtie-icon{font-size:unset}.markdown-content h5{font-size:.75rem;font-weight:600;color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));margin:24px 0 8px 0}.markdown-content h5 .bowtie-icon{font-size:unset}.markdown-content h6{font-size:.75rem;font-weight:600;color:rgba(0,0,0,.55);color:var(--text-secondary-color,rgba(0, 0, 0, .55));margin:24px 0 8px 0}.markdown-content h6 .bowtie-icon{font-size:unset}.bolt-focus-visible .markdown-content a:focus{outline:rgba(0,0,0,.6) solid 1px;outline:var(--palette-black-alpha-60,rgba(0, 0, 0, .6)) solid 1px}.hljs{display:block;overflow-x:auto;padding:.5em;color:rgba(0,0,0,.9);color:var(--text-primary-color,rgba(0, 0, 0, .9));-webkit-text-size-adjust:none}.markdown-preview-container.markdown-content{margin:20px}h1:hover a.shareHeaderAnchor:after,h2:hover a.shareHeaderAnchor:after,h3:hover a.shareHeaderAnchor:after,h4:hover a.shareHeaderAnchor:after,h5:hover a.shareHeaderAnchor:after,h6:hover a.shareHeaderAnchor:after{content:\"\\e71b \";font-family:\"AzureDevOpsMDL2Assets\";font-size:smaller}h1:hover a.shareHeaderAnchor:hover,h2:hover a.shareHeaderAnchor:hover,h3:hover a.shareHeaderAnchor:hover,h4:hover a.shareHeaderAnchor:hover,h5:hover a.shareHeaderAnchor:hover,h6:hover a.shareHeaderAnchor:hover{text-decoration:none}h1 a.shareHeaderAnchor,h2 a.shareHeaderAnchor,h3 a.shareHeaderAnchor,h4 a.shareHeaderAnchor,h5 a.shareHeaderAnchor,h6 a.shareHeaderAnchor{margin:0 4px 0 4px;padding:0 4px 0 4px;font-weight:normal;text-decoration:none}h1 a.shareHeaderAnchor:focus:after,h2 a.shareHeaderAnchor:focus:after,h3 a.shareHeaderAnchor:focus:after,h4 a.shareHeaderAnchor:focus:after,h5 a.shareHeaderAnchor:focus:after,h6 a.shareHeaderAnchor:focus:after{content:\"\\e71b \";font-family:\"AzureDevOpsMDL2Assets\";font-size:smaller}h1 a.shareHeaderAnchor,h2 a.shareHeaderAnchor{line-height:22px}h3 a.shareHeaderAnchor,h4 a.shareHeaderAnchor{line-height:18px}h5 a.shareHeaderAnchor,h6 a.shareHeaderAnchor{line-height:14px}table.metadata-yaml-table{font-size:.75rem;line-height:1}.toc-container{border:1px solid;border-color:rgba(234,234,234,1);border-color:rgba(var(--palette-neutral-8,234, 234, 234),1);border-radius:4px;display:inline-block;padding:10px 16px 10px 0;margin-bottom:14px;min-width:250px}.toc-container .toc-container-header{font-weight:600;margin:0 16px 5px 16px}.toc-container ul{list-style:none;margin:0;padding-left:16px;color:rgba(0,90,158,1);color:var(--communication-foreground,rgba(0, 90, 158, 1))}.toc-container ul li{margin-top:4px;margin-bottom:0}.toc-container li::before{content:\"\\2022 \"}.toc-container a{margin-left:5px;display:inline-block;vertical-align:top;width:auto;max-width:400px;text-decoration:none;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;word-wrap:normal}.task-list input.task-list-item-checkbox{vertical-align:middle;margin:4px;float:none}ul li.task-list-item{list-style-type:none}ul.task-list{padding-left:7px}li.task-list-item{min-height:18px}";
            var html = $"<html><head><title></title><style>{markdownStyle}</style></head><body><div class=\"markdown-content markdown-render-area body-m\">{markdownHtml}</div></body></html>";
            return new ContentResult
            {
                Content = html,
                ContentType = "text/html",
                StatusCode = 200
            };
        }
    }
}