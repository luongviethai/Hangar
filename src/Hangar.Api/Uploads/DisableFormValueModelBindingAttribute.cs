using Microsoft.AspNetCore.Mvc.Filters;

namespace Hangar.Api.Uploads;

// FormValueProviderFactory (đăng ký sẵn bởi AddControllers()) tự gọi Request.ReadFormAsync() cho
// MỌI request Content-Type multipart/form-data, dù action không có tham số [FromForm] nào — nó
// phải dựng value provider trước khi MVC biết action cần binding gì. Việc đó đọc sạch
// Request.Body trước khi action code (đọc Body thủ công bằng MultipartReader) kịp chạy, gây lỗi
// "Unexpected end of Stream, the content may have already been read by another component."
// Attribute này gỡ đúng các value-provider-factory đó khỏi pipeline cho riêng action được đánh dấu.
public class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        for (var i = factories.Count - 1; i >= 0; i--)
        {
            var typeName = factories[i].GetType().Name;
            if (typeName is "FormValueProviderFactory" or "FormFileValueProviderFactory" or "JQueryFormValueProviderFactory")
            {
                factories.RemoveAt(i);
            }
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
