document.addEventListener("DOMContentLoaded", function() {
    
    // 1. Xử lý hiệu ứng cuộn chuột (Scroll Reveal)
    const reveals = document.querySelectorAll(".reveal");
    const revealOnScroll = () => {
        let windowHeight = window.innerHeight;
        let elementVisible = 100; // Cách đáy màn hình 100px thì bắt đầu hiện
        
        reveals.forEach((reveal) => {
            let elementTop = reveal.getBoundingClientRect().top;
            if (elementTop < windowHeight - elementVisible) {
                reveal.classList.add("active");
            }
        });
    };
    
    window.addEventListener("scroll", revealOnScroll);
    revealOnScroll(); // Gọi ngay lần đầu để hiện các phần tử đang ở trên cùng

    // 2. Chuyển trang mượt mà (Smooth Page Transition)
    const links = document.querySelectorAll('a[href]:not([target="_blank"])');
    links.forEach(link => {
        link.addEventListener('click', function(e) {
            const href = this.getAttribute('href');
            // Bỏ qua nếu là link anchor (#) hoặc link trống
            if (!href || href.startsWith('#') || href.includes('javascript')) return;
            
            e.preventDefault(); // Chặn chuyển trang ngay lập tức
            
            // Làm mờ body hiện tại
            document.body.style.transition = "opacity 0.4s ease, transform 0.4s ease";
            document.body.style.opacity = "0";
            document.body.style.transform = "translateY(-10px)";
            
            // Chờ 0.4s (bằng thời gian transition) rồi mới nhảy sang link mới
            setTimeout(() => {
                window.location.href = href;
            }, 400);
        });
    });
});