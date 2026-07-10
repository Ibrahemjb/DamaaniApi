namespace DammaniAPI.Features.Messaging;

// BP §12 default WhatsApp click-to-send templates, verbatim (AR + EN).
// These are the platform defaults; DMN-901/903 add per-shop MessageTemplate
// rows that override them. Screens must never hardcode message text (DMN-410).
public static class DefaultMessages
{
    public class MessageText
    {
        public string Ar { get; set; } = "";
        public string En { get; set; } = "";
    }

    public static class Keys
    {
        public const string WarrantyCreated = "warranty_created";
        public const string RequestReceived = "request_received";
        public const string StatusReviewing = "status_reviewing";
        public const string StatusWaitingCustomer = "status_waiting_customer";
        public const string StatusSentSupplier = "status_sent_supplier";
        public const string StatusRepaired = "status_repaired";
        public const string StatusReplaced = "status_replaced";
        public const string StatusRejected = "status_rejected";
        public const string StatusClosed = "status_closed";
    }

    public static readonly IReadOnlyDictionary<string, MessageText> Defaults = new Dictionary<string, MessageText>
    {
        [Keys.WarrantyCreated] = new()
        {
            Ar = "مرحباً {customer_name}\nتم إنشاء كرت الضمان الإلكتروني للمنتج: {product_name}\nرقم الضمان: {warranty_code}\nينتهي الضمان بتاريخ: {expiry_date}\nرابط الضمان: {public_link}\nشكراً لاختياركم {shop_name}",
            En = "Hello {customer_name},\nYour digital warranty card has been created for: {product_name}\nWarranty code: {warranty_code}\nWarranty expires on: {expiry_date}\nView warranty: {public_link}\nThank you for choosing {shop_name}."
        },
        [Keys.RequestReceived] = new()
        {
            Ar = "تم استلام طلب الصيانة رقم {request_number} للمنتج {product_name}. سنقوم بمراجعته والتواصل معك.",
            En = "Your service request #{request_number} for {product_name} has been received. We will review it and contact you."
        },
        [Keys.StatusReviewing] = new()
        {
            Ar = "طلبك قيد المراجعة.",
            En = "Your request is under review."
        },
        [Keys.StatusWaitingCustomer] = new()
        {
            Ar = "نحتاج معلومات إضافية بخصوص طلبك.",
            En = "We need more information about your request."
        },
        [Keys.StatusSentSupplier] = new()
        {
            Ar = "تم تحويل الطلب للمورّد / مركز الصيانة.",
            En = "Your request was sent to the supplier/service center."
        },
        [Keys.StatusRepaired] = new()
        {
            Ar = "تم إصلاح المنتج، يرجى التواصل لاستلامه.",
            En = "Your product has been repaired. Please contact us for pickup."
        },
        // BP §12 has no "replaced" text; adapted from the Repaired message with a
        // single word change. Flagged for product review (DMN-410 spec note).
        [Keys.StatusReplaced] = new()
        {
            Ar = "تم استبدال المنتج، يرجى التواصل لاستلامه.",
            En = "Your product has been replaced. Please contact us for pickup."
        },
        [Keys.StatusRejected] = new()
        {
            Ar = "نعتذر، الطلب غير مشمول بالضمان حسب الشروط.",
            En = "Sorry, this request is not covered under the warranty terms."
        },
        // BP §12 has no "closed" text; minimal neutral text including the request
        // number (BP §12: always include warranty code or request number).
        // Flagged for product review (DMN-410 spec note).
        [Keys.StatusClosed] = new()
        {
            Ar = "تم إغلاق طلب الصيانة رقم {request_number}. شكراً لتواصلكم معنا.",
            En = "Your service request #{request_number} has been closed. Thank you for contacting us."
        }
    };
}
