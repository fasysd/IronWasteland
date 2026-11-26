/* quiz.js — thành phần trắc nghiệm dùng chung cho các bài học.
   Cách dùng (xem lessons/0001-*.html):

   <div class="quiz" data-quiz>
     <fieldset class="q" data-answer="1" data-explain="Giải thích ngắn vì sao.">
       <legend><span class="num">1</span> Câu hỏi?</legend>
       <label class="opt"><input type="radio" name="q1" value="0"><span>Đáp án A</span></label>
       <label class="opt"><input type="radio" name="q1" value="1"><span>Đáp án B</span></label>
       <label class="opt"><input type="radio" name="q1" value="2"><span>Đáp án C</span></label>
       <p class="fb" aria-live="polite"></p>
     </fieldset>
     <div class="quiz-bar">
       <button class="btn" type="button" data-reset>Làm lại</button>
       <span class="quiz-score" aria-live="polite"></span>
     </div>
   </div>

   Phản hồi hiện ngay khi chọn (vòng phản hồi càng ngắn càng tốt), không cần bấm nộp. */

(function () {
  "use strict";

  function init(root) {
    var questions = Array.prototype.slice.call(root.querySelectorAll(".q[data-answer]"));
    var scoreEl = root.querySelector(".quiz-score");
    var resetBtn = root.querySelector("[data-reset]");

    function update() {
      var answered = 0;
      var correct = 0;

      questions.forEach(function (q) {
        var chosen = q.querySelector("input[type=radio]:checked");
        var fb = q.querySelector(".fb");

        q.querySelectorAll("label.opt").forEach(function (label) {
          label.classList.remove("is-right", "is-wrong");
        });

        if (!chosen) {
          if (fb) {
            fb.textContent = "";
            fb.className = "fb";
          }
          return;
        }

        answered += 1;
        var okay = chosen.value === q.dataset.answer;
        var chosenLabel = chosen.closest("label");
        if (chosenLabel) chosenLabel.classList.add(okay ? "is-right" : "is-wrong");

        if (!okay) {
          var right = q.querySelector('input[type=radio][value="' + q.dataset.answer + '"]');
          if (right && right.closest("label")) right.closest("label").classList.add("is-right");
        } else {
          correct += 1;
        }

        if (fb) {
          fb.textContent = (okay ? "Đúng. " : "Chưa đúng. ") + (q.dataset.explain || "");
          fb.className = "fb " + (okay ? "fb--right" : "fb--wrong");
        }
      });

      if (scoreEl) {
        if (!answered) {
          scoreEl.textContent = "";
        } else if (answered < questions.length) {
          scoreEl.textContent = correct + "/" + answered + " câu đúng";
        } else {
          scoreEl.textContent = correct + "/" + questions.length + " câu đúng — hết bộ câu hỏi.";
        }
      }
    }

    root.addEventListener("change", function (event) {
      if (event.target && event.target.type === "radio") update();
    });

    if (resetBtn) {
      resetBtn.addEventListener("click", function () {
        root.querySelectorAll("input[type=radio]").forEach(function (input) {
          input.checked = false;
        });
        questions.forEach(function (q) {
          var fb = q.querySelector(".fb");
          if (fb) {
            fb.textContent = "";
            fb.className = "fb";
          }
        });
        update();
      });
    }

    update();
  }

  document.querySelectorAll("[data-quiz]").forEach(init);
})();
