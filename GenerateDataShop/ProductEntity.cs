namespace GenerateDataShop
{
    public class ProductEntity : BaseEntity
    {
        public string pName { get; set; }

        public string pDescription { get; set; }

        public int pPrice { get; set; }

        public int pSold { get; set; }

        public int pQuantity { get; set; }

        public string pCategory { get; set; }

        public List<string> pImages { get; set; }

        public string pOffer { get; set; }

        public List<RatingReviewEntity> pRatingReviews { get; set; }

        public string pStatus { get; set; }
    }

    public class RatingReviewEntity
    {
        public string Review { get; set; }

        public string User { get; set; }

        public string Rating { get; set; }

        public string CreateAt { get; set; }
    }
}
