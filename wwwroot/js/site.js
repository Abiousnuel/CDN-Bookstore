
document.addEventListener("DOMContentLoaded", function () {

    const CART_KEY = "cdnShoppingCart";

    // Get cart from browser storage
    function getCart() {
        try {
            return JSON.parse(localStorage.getItem(CART_KEY)) || [];
        } catch {
            return [];
        }
    }

    // Save cart
    function saveCart(cart) {
        localStorage.setItem(CART_KEY, JSON.stringify(cart));
    }

    // Format prices in Nigerian Naira
    function formatPrice(price) {
        return "₦" + Number(price).toLocaleString("en-NG", {
            maximumFractionDigits: 2
        });
    }

    // Update cart count in navigation
    function updateCartCount() {
        const countElement = document.getElementById("cartCount");

        if (!countElement) return;

        const cart = getCart();
        const totalItems = cart.reduce(
            (total, item) => total + item.quantity, 0
        );

        countElement.textContent = totalItems;
    }

    // Add book to cart
    document.addEventListener("click", function (event) {

        const button = event.target.closest(".add-cart-btn");

        if (!button) return;

        const id = button.dataset.id;
        const title = button.dataset.title;
        const price = Number(button.dataset.price);
        const image = button.dataset.image ||
            "/images/books/default-cover.png";

        if (!id || !title || !Number.isFinite(price) || price <= 0) {
            alert("Unable to add this book. Please try again.");
            return;
        }

        const cart = getCart();
        const existingBook = cart.find(item => item.id === id);

        if (existingBook) {
            existingBook.quantity += 1;
        } else {
            cart.push({
                id: id,
                title: title,
                price: price,
                image: image,
                quantity: 1
            });
        }

        saveCart(cart);
        updateCartCount();

        const originalText = button.textContent;
        button.textContent = "Added to Cart ✓";

        setTimeout(() => {
            button.textContent = originalText;
        }, 1500);

        renderCart();
    });

    // Display cart items
    function renderCart() {
        const cartItems = document.getElementById("cartItems");
        const emptyCart = document.getElementById("emptyCart");

        if (!cartItems || !emptyCart) return;

        const cart = getCart();

        cartItems.innerHTML = "";

        if (cart.length === 0) {
            emptyCart.hidden = false;
            updateCartTotals(0);
            return;
        }

        emptyCart.hidden = true;

        cart.forEach(item => {

            const itemContainer = document.createElement("div");
            itemContainer.className =
                "card mb-3 p-3";

            const row = document.createElement("div");
            row.className = "row align-items-center g-3";

            const imageColumn = document.createElement("div");
            imageColumn.className = "col-4 col-md-3";

            const image = document.createElement("img");
            image.src = item.image ||
                "/images/books/default-cover.png";
            image.alt = item.title;
            image.className = "img-fluid rounded";
            image.style.maxHeight = "130px";
            image.style.objectFit = "contain";

            image.onerror = function () {
                this.onerror = null;
                this.src = "/images/books/default-cover.png";
            };

            imageColumn.appendChild(image);

            const detailsColumn = document.createElement("div");
            detailsColumn.className = "col-8 col-md-9";

            const title = document.createElement("h5");
            title.textContent = item.title;

            const price = document.createElement("p");
            price.textContent = formatPrice(item.price);

            const quantity = document.createElement("div");
            quantity.className =
                "d-flex align-items-center gap-2 flex-wrap";

            const decrease = document.createElement("button");
            decrease.className = "btn btn-outline-secondary btn-sm";
            decrease.textContent = "−";
            decrease.dataset.action = "decrease";
            decrease.dataset.id = item.id;

            const quantityText = document.createElement("span");
            quantityText.textContent = item.quantity;

            const increase = document.createElement("button");
            increase.className = "btn btn-outline-secondary btn-sm";
            increase.textContent = "+";
            increase.dataset.action = "increase";
            increase.dataset.id = item.id;

            const remove = document.createElement("button");
            remove.className = "btn btn-outline-danger btn-sm";
            remove.textContent = "Remove";
            remove.dataset.action = "remove";
            remove.dataset.id = item.id;

            quantity.append(
                decrease,
                quantityText,
                increase,
                remove
            );

            const itemTotal = document.createElement("strong");
            itemTotal.textContent =
                "Total: " + formatPrice(item.price * item.quantity);

            detailsColumn.append(
                title,
                price,
                quantity,
                itemTotal
            );

            row.append(imageColumn, detailsColumn);
            itemContainer.appendChild(row);
            cartItems.appendChild(itemContainer);
        });

        const subtotal = cart.reduce(
            (total, item) => total + item.price * item.quantity, 0
        );

        updateCartTotals(subtotal);
    }

    // Update subtotal and total
    function updateCartTotals(subtotal) {
        const subtotalElement =
            document.getElementById("cartSubtotal");

        const totalElement =
            document.getElementById("cartTotal");

        if (subtotalElement) {
            subtotalElement.textContent = formatPrice(subtotal);
        }

        if (totalElement) {
            totalElement.textContent = formatPrice(subtotal);
        }
    }

    // Increase, decrease or remove cart items
    document.addEventListener("click", function (event) {

        const button = event.target.closest("[data-action]");

        if (!button) return;

        const action = button.dataset.action;
        const id = button.dataset.id;

        if (!["increase", "decrease", "remove"].includes(action)) {
            return;
        }

        let cart = getCart();
        const item = cart.find(book => book.id === id);

        if (!item) return;

        if (action === "increase") {
            item.quantity += 1;
        }

        if (action === "decrease") {
            item.quantity -= 1;

            if (item.quantity <= 0) {
                cart = cart.filter(book => book.id !== id);
            }
        }

        if (action === "remove") {
            cart = cart.filter(book => book.id !== id);
        }

        saveCart(cart);
        renderCart();
        updateCartCount();
    });

    // Checkout button
    // Checkout button
    const checkoutButton = document.getElementById("checkoutBtn");

    if (checkoutButton) {
        checkoutButton.addEventListener("click", async function () {
            const cart = getCart();

            if (cart.length === 0) {
                alert("Your shopping cart is empty.");
                return;
            }

            const token = document.querySelector(
                '#checkoutAntiForgeryForm input[name="__RequestVerificationToken"]'
            )?.value;

            if (!token) {
                alert("Security token missing. Please refresh the page.");
                return;
            }

            const originalText = checkoutButton.textContent;
            checkoutButton.disabled = true;
            checkoutButton.textContent = "Preparing checkout...";

            try {
                const response = await fetch("/Cart/Checkout", {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": token
                    },
                    body: JSON.stringify({
                        items: cart.map(item => ({
                            bookId: Number(item.id),
                            quantity: Number(item.quantity)
                        }))
                    })
                });

                if (response.redirected) {
                    window.location.href =
                        "/Account/Login?ReturnUrl=%2FCart";
                    return;
                }

                const result = await response.json();

                if (!response.ok) {
                    throw new Error(
                        result.message || "Checkout could not be started."
                    );
                }

                const checkoutUrl = new URL(result.checkoutUrl);

                if (
                    checkoutUrl.protocol !== "https:" ||
                    checkoutUrl.hostname !== "checkout.paystack.com"
                ) {
                    throw new Error("Invalid Paystack checkout address.");
                }

                window.location.href = checkoutUrl.href;
            }
            catch (error) {
                alert(error.message || "An error occurred during checkout.");
                checkoutButton.disabled = false;
                checkoutButton.textContent = originalText;
            }
        });
    }

   // Clear cart only after successful payment verification
const urlParams = new URLSearchParams(window.location.search);

if (urlParams.get("paymentSuccess")?.toLowerCase() === "true") 
    {    localStorage.removeItem(CART_KEY);

    // Remove the payment indicator so refreshing won't clear a new cart.
    window.history.replaceState(
        {},
        document.title,
        window.location.pathname + window.location.hash
    );
}

// Initialize cart
updateCartCount();
renderCart();

});
