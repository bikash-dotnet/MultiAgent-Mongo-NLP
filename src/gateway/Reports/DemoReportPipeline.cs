namespace Gateway.Reports;

public static class DemoReportPipeline
{
    public static string FromUtterance(string utterance)
    {
        return """[{"$match":{"address.market":"New York"}},{"$group":{"_id":"$address.market","avg_price":{"$avg":"$price"},"count":{"$sum":1}}},{"$sort":{"avg_price":-1}},{"$limit":10}]""";
    }
}
